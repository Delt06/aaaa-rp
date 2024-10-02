Shader "Hidden/AAAA/LightingDebug"
{
    HLSLINCLUDE
        #pragma target 2.0
        #pragma editor_sync_compilation
        
        #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "AAAAPipeline" }

        Pass
        {
            ZWrite Off
            ZTest Off
            ZClip Off
            Cull Off
            Blend One One
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/CameraDepth.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Math.hlsl"
            #include "Packages/com.deltation.aaaa-rp/Runtime/Debugging/AAAADebugDisplaySettingsRendering.cs.hlsl"

            uint _LightingDebugMode;
            float2 _LightingDebugCountRemap;

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

            #define OVERLAY_OPACITY 0.25f

            float3 LightCountHeatmap(float t)
            {
                const float3 c0 = float3(0.0f, 0.0f, 1);
                const float3 c1 = float3(1, 0.0f, 0.0f);
                return lerp(c0, c1, t);
            }

            float4 Frag(const Varyings IN) : SV_Target
            {
                float3 resultColor = 0;

                const float  deviceDepth = SampleDeviceDepth(IN.texcoord);
                const float3 positionWS = ComputeWorldSpacePosition(IN.texcoord, deviceDepth, UNITY_MATRIX_I_VP);
                const float2 screenUV = IN.texcoord;

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
                        const LightGridCell lightGridCell = ClusteredLighting::LoadCell(positionWS, screenUV);

                        if (lightGridCell.count > 0)
                        {
                            const float2 remap = _LightingDebugCountRemap;
                            const float remappedLightCount = InverseLerpUnclamped(remap.x, remap.y,lightGridCell.count);
                            resultColor = LightCountHeatmap(remappedLightCount);
                        }

                        break;
                    }
                default:
                    {
                        break;
                    }
                }

                return float4(OVERLAY_OPACITY * resultColor, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}