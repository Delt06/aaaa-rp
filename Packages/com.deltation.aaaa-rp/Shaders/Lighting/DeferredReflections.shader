Shader "Hidden/AAAA/DeferredReflections"
{
    Properties {}

    HLSLINCLUDE
    #pragma target 5.0
    #pragma editor_sync_compilation

    #include_with_pragmas "Packages/com.deltation.aaaa-rp/ShaderLibrary/Bindless.hlsl"
    #include_with_pragmas "Packages/com.deltation.aaaa-rp/Shaders/GlobalIllumination/GTAOPragma.hlsl"

    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    #include "Packages/com.deltation.aaaa-rp/Runtime/AAAAStructs.cs.hlsl"
    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/CameraDepth.hlsl"
    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/GBuffer.hlsl"
    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/GTAO.hlsl"
    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Lighting.hlsl"
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

    Varyings OverrideVert(Attributes input)
    {
        Varyings output = Vert(input);

        output.positionCS.z = UNITY_RAW_FAR_CLIP_VALUE * output.positionCS.w;

        return output;
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "AAAAPipeline"
        }
        ZWrite Off
        ZTest Greater
        ZClip Off
        Cull Off

        Pass
        {
            Name "Deferred Reflections: Environment"

            Blend One One

            HLSLPROGRAM
            #pragma vertex OverrideVert
            #pragma fragment Frag

            #include_with_pragmas "Packages/com.deltation.aaaa-rp/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #include_with_pragmas "Packages/com.deltation.aaaa-rp/Shaders/GlobalIllumination/LPV/SkyOcclusionVariants.hlsl"

            float4 Frag(const Varyings IN) : SV_Target
            {
                const GBufferValue gbuffer = SampleGBuffer(IN.texcoord);
                if (gbuffer.materialFlags & AAAAMATERIALFLAGS_UNLIT)
                {
                    return 0;
                }

                const float deviceDepth = SampleDeviceDepth(IN.texcoord);
                SurfaceData surfaceData;
                InitializeSurfaceData(gbuffer, IN, deviceDepth, surfaceData);

                const float3 cameraPositionWS = GetCameraPositionWS();

                const float3 eyeWS = normalize(cameraPositionWS - surfaceData.positionWS);
                const float3 reflectionWS = ComputeBRDFReflectionVector(surfaceData.bentNormalWS, eyeWS);
                const float  skyOcclusion = SampleSkyOcclusion(surfaceData.positionWS, surfaceData.bentNormalWS, eyeWS, reflectionWS, 0xFFFFFFFFu);
                return float4(skyOcclusion * aaaa_AmbientIntensity * SamplePrefilteredEnvironment(reflectionWS, surfaceData.roughness), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Deferred Reflections: Compose"

            Blend One One

            HLSLPROGRAM
            #pragma vertex OverrideVert
            #pragma fragment Frag

            static float3 ComputeLightingIndirect(const SurfaceData surfaceData, const float3 reflections)
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
                brdfInput.prefilteredEnvironment = reflections;

                const float3 eyeWS = normalize(brdfInput.cameraPositionWS - surfaceData.positionWS);
                const float3 indirectSpecular = ComputeBRDFIndirectSpecular(brdfInput, eyeWS);
                return indirectSpecular;
            }

            float4 Frag(const Varyings IN) : SV_Target
            {
                const GBufferValue gbuffer = SampleGBuffer(IN.texcoord);
                if (gbuffer.materialFlags & AAAAMATERIALFLAGS_UNLIT)
                {
                    return 0;
                }

                const float deviceDepth = SampleDeviceDepth(IN.texcoord);
                SurfaceData surfaceData;
                InitializeSurfaceData(gbuffer, IN, deviceDepth, surfaceData);

                const float3 reflections = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, IN.texcoord).rgb;
                const float3 lighting = ComputeLightingIndirect(surfaceData, reflections);
                return float4(lighting, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}