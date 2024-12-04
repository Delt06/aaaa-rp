Shader "Hidden/AAAA/LightingDebug"
{
    HLSLINCLUDE
    #pragma editor_sync_compilation

    #include_with_pragmas "Packages/com.deltation.aaaa-rp/ShaderLibrary/Bindless.hlsl"
    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "AAAAPipeline"
        }

        Pass
        {
            ZWrite Off
            ZTest Off
            ZClip Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include_with_pragmas "Packages/com.deltation.aaaa-rp/Shaders/GlobalIllumination/GTAOPragma.hlsl"
            #include_with_pragmas "Packages/com.deltation.aaaa-rp/Shaders/GlobalIllumination/LPVPragma.hlsl"
            #include_with_pragmas "Packages/com.deltation.aaaa-rp/ShaderLibrary/ProbeVolumeVariants.hlsl"

            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/CameraDepth.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/GBuffer.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Math.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/GTAO.hlsl"
            #include "Packages/com.deltation.aaaa-rp/Runtime/Debugging/AAAADebugDisplaySettingsRendering.cs.hlsl"

            uint   _LightingDebugMode;
            float2 _LightingDebugCountRemap;
            uint   _LightIndex;

            TEXTURE2D(_IndirectSpecular);

            #define LIGHT_POOL_SIZE 6

            #define DIM_COLOR 0.1
            #define BRIGHT_COLOR 0.8

            static float3 lightPool[] = {
                float3(DIM_COLOR, DIM_COLOR, BRIGHT_COLOR),
                float3(DIM_COLOR, BRIGHT_COLOR, DIM_COLOR),
                float3(DIM_COLOR, BRIGHT_COLOR, BRIGHT_COLOR),
                float3(BRIGHT_COLOR, DIM_COLOR, DIM_COLOR),
                float3(BRIGHT_COLOR, DIM_COLOR, BRIGHT_COLOR),
                float3(BRIGHT_COLOR, BRIGHT_COLOR, DIM_COLOR),
            };

            #define OVERLAY_OPACITY 0.5f

            float3 LightCountHeatmap(float t)
            {
                const float3 c0 = float3(0.0f, 0.0f, 1.0f);
                const float3 c1 = float3(0.0f, 1.0f, 0.0f);
                const float3 c2 = float3(1.0f, 0.0f, 0.0f);
                if (t <= 0.5)
                {
                    return lerp(c0, c1, t / 0.5);
                }
                return lerp(c1, c2, t / 0.5 - 1);
            }

            float4 Frag(const Varyings IN) : SV_Target
            {
                float3 resultColor = 0;
                float  resultOpacity = OVERLAY_OPACITY;

                const float2 screenUV = IN.texcoord;
                const float  deviceDepth = SampleDeviceDepth(screenUV);
                const float3 positionWS = ComputeWorldSpacePosition(screenUV, deviceDepth, UNITY_MATRIX_I_VP);

                switch (_LightingDebugMode)
                {
                case AAAALIGHTINGDEBUGMODE_CLUSTER_Z:
                    {
                        if (deviceDepth != UNITY_RAW_FAR_CLIP_VALUE)
                        {
                            const float3 positionVS = TransformWorldToView(positionWS);
                            const uint   flatClusterIndex = ClusteredLighting::NormalizedScreenUVToFlatClusterIndex(screenUV, positionVS.z);
                            const uint3  clusterIndex = ClusteredLightingCommon::UnflattenClusterIndex(flatClusterIndex);
                            resultColor = lightPool[clusterIndex.z % LIGHT_POOL_SIZE];
                        }
                        break;
                    }
                case AAAALIGHTINGDEBUGMODE_DEFERRED_LIGHTS:
                    {
                        const AAAAClusteredLightingGridCell lightGridCell = ClusteredLighting::LoadCell(positionWS, screenUV);

                        if (lightGridCell.Count > 0)
                        {
                            const float2 remap = _LightingDebugCountRemap;
                            const float  remappedLightCount = InverseLerpUnclamped(remap.x, remap.y, lightGridCell.Count);
                            resultColor = LightCountHeatmap(saturate(remappedLightCount));
                        }

                        break;
                    }
                case AAAALIGHTINGDEBUGMODE_DIRECTIONAL_LIGHT_CASCADES:
                    {
                        if (_LightIndex < DirectionalLightCount)
                        {
                            const float4 shadowSliceRange_shadowFadeParams = DirectionalLightShadowSliceRanges_ShadowFadeParams[_LightIndex];
                            const CascadedDirectionalLightShadowSample shadowSample = SampleCascadedDirectionalLightShadow(
                                positionWS, shadowSliceRange_shadowFadeParams.xy, shadowSliceRange_shadowFadeParams.zw, false);
                            if (shadowSample.cascadeIndex != -1)
                            {
                                resultColor = lightPool[shadowSample.cascadeIndex % LIGHT_POOL_SIZE];
                                resultColor = lerp(resultColor, 0, shadowSample.shadowFade);
                            }
                        }

                        break;
                    }
                case AAAALIGHTINGDEBUGMODE_AMBIENT_OCCLUSION:
                case AAAALIGHTINGDEBUGMODE_BENT_NORMALS:
                    {
                        const float3 normalWS = 0;
                        float        visibility = 0;
                        float3       bentNormals;
                        SampleGTAO(IN.positionCS.xy, normalWS, visibility, bentNormals);

                        resultColor = _LightingDebugMode == AAAALIGHTINGDEBUGMODE_BENT_NORMALS ? bentNormals * 0.5 + 0.5 : visibility;
                        resultOpacity = 1;
                        break;
                    }
                case AAAALIGHTINGDEBUGMODE_INDIRECT_DIFFUSE:
                    {
                        const GBufferValue gbufferValue = SampleGBuffer(screenUV);
                        const float3       eyeWS = normalize(GetCameraPositionWS() - positionWS);
                        resultColor = SampleDiffuseGI(positionWS, gbufferValue.normalWS, eyeWS, IN.positionCS.xy, 0xFFFFFFFFu);
                        resultOpacity = 1;
                        break;
                    }
                case AAAALIGHTINGDEBUGMODE_INDIRECT_SPECULAR:
                    {
                        resultColor = SAMPLE_TEXTURE2D(_IndirectSpecular, sampler_LinearClamp, screenUV);
                        resultOpacity = 1;
                        break;
                    }
                default:
                    {
                        break;
                    }
                }

                return float4(resultColor, resultOpacity);
            }
            ENDHLSL
        }
    }

    Fallback Off
}