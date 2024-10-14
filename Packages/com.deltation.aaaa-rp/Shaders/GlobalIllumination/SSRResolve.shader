Shader "Hidden/AAAA/SSR/Resolve"
{
    Properties {}

    HLSLINCLUDE
    #pragma target 5.0
    #pragma editor_sync_compilation

    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

    Varyings OverrideVert(Attributes input)
    {
        Varyings output = Vert(input);

        output.positionCS.z = UNITY_RAW_FAR_CLIP_VALUE * output.positionCS.w;

        return output;
    }
    ENDHLSL

    SubShader
    {
        ZWrite Off
        ZTest Greater
        ZClip Off
        Cull Off

        Pass
        {
            Name "SSR Resolve: Resolve UV"


            HLSLPROGRAM
            #pragma vertex OverrideVert
            #pragma fragment Frag

            TEXTURE2D(_SSRTraceResult);
            TEXTURE2D(_CameraColor);

            float4 Frag(const Varyings IN) : SV_Target
            {
                const float4 traceValue = SAMPLE_TEXTURE2D(_SSRTraceResult, sampler_LinearClamp, IN.texcoord);
                if (all(traceValue == 0))
                {
                    return 0;
                }

                const float3 reflection = SAMPLE_TEXTURE2D(_CameraColor, sampler_LinearClamp, traceValue.xy).rgb;
                return float4(reflection, traceValue.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "SSR Resolve: Compose"

            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex OverrideVert
            #pragma fragment Frag

            TEXTURE2D(_SSRResolveResult);

            #include_with_pragmas "Packages/com.deltation.aaaa-rp/ShaderLibrary/Bindless.hlsl"
            #include_with_pragmas "Packages/com.deltation.aaaa-rp/Shaders/GlobalIllumination/GTAOPragma.hlsl"

            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/CameraDepth.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/GBuffer.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/PBR.hlsl"

            void InitializeSurfaceData(const GBufferValue gbuffer,
                                       const Varyings     IN,
                                       const float        deviceDepth,
                                       out SurfaceData    surfaceData)
            {
                surfaceData.albedo = gbuffer.albedo;
                surfaceData.roughness = gbuffer.roughness;
                surfaceData.metallic = gbuffer.metallic;
                surfaceData.normalWS = gbuffer.normalWS;
                surfaceData.positionWS = ComputeWorldSpacePosition(IN.texcoord, deviceDepth, UNITY_MATRIX_I_VP);

                SampleGTAO(IN.positionCS.xy, surfaceData.normalWS, surfaceData.aoVisibility, surfaceData.bentNormalWS);
            }

            static float3 ComputeLightingIndirect(const SurfaceData surfaceData, const float3 ssrColor)
            {
                BRDFInput brdfInput;
                brdfInput.normalWS = surfaceData.normalWS;
                brdfInput.positionWS = surfaceData.positionWS;
                brdfInput.cameraPositionWS = GetCameraPositionWS();
                brdfInput.diffuseColor = surfaceData.albedo;
                brdfInput.metallic = surfaceData.metallic;
                brdfInput.roughness = surfaceData.roughness;
                brdfInput.irradiance = 0;
                brdfInput.aoVisibility = surfaceData.aoVisibility;
                brdfInput.bentNormalWS = surfaceData.bentNormalWS;

                const float3 eyeWS = normalize(brdfInput.cameraPositionWS - surfaceData.positionWS);
                brdfInput.prefilteredEnvironment = ssrColor.rgb;
                const float3 indirectSpecular = ComputeBRDFIndirectSpecular(brdfInput, eyeWS);
                return aaaa_AmbientIntensity * indirectSpecular;
            }

            float4 Frag(const Varyings IN) : SV_Target
            {
                const float        deviceDepth = SampleDeviceDepth(IN.texcoord);
                const GBufferValue gbuffer = SampleGBuffer(IN.texcoord);
                SurfaceData        surfaceData;
                InitializeSurfaceData(gbuffer, IN, deviceDepth, surfaceData);

                const float4 ssrValue = SAMPLE_TEXTURE2D(_SSRResolveResult, sampler_LinearClamp, IN.texcoord);
                const float  roughnessAttenuation = 1.0 / (surfaceData.roughness * surfaceData.roughness + 1.0);
                const float3 lightingIndirect = ComputeLightingIndirect(surfaceData, ssrValue.rgb);

                float contribution = ssrValue.a * roughnessAttenuation;
                // Modulate reflection color to fix black halos from low intesity reflection colors.
                // contribution *= saturate(length(ssrValue.rgb));
                contribution *= surfaceData.metallic;
                return float4(lightingIndirect, contribution);
            }
            ENDHLSL
        }
    }
}