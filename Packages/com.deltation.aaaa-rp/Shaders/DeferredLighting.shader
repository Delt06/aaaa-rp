Shader "Hidden/AAAA/DeferredLighting"
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
    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/CameraDepth.hlsl"
    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/GBuffer.hlsl"
    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/GTAO.hlsl"
    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/PBR.hlsl"

    void InitializeSurfaceData(const GBufferValue gbuffer,
                           const Varyings         IN,
                           const float            deviceDepth,
                           out SurfaceData        surfaceData)
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
        Blend One One

        Pass
        {
            Name "Deferred Lighting: Direct"


            HLSLPROGRAM
            #pragma vertex OverrideVert
            #pragma fragment Frag

            float4 Frag(const Varyings IN) : SV_Target
            {
                const float        deviceDepth = SampleDeviceDepth(IN.texcoord);
                const GBufferValue gbuffer = SampleGBuffer(IN.texcoord);
                SurfaceData        surfaceData;
                InitializeSurfaceData(gbuffer, IN, deviceDepth, surfaceData);

                float3     lighting = 0;
                const uint lightCount = GetDirectionalLightCount();

                BRDFInput brdfInput;
                brdfInput.normalWS = surfaceData.normalWS;
                brdfInput.positionWS = surfaceData.positionWS;
                brdfInput.cameraPositionWS = GetCameraPositionWS();
                brdfInput.diffuseColor = surfaceData.albedo;
                brdfInput.metallic = surfaceData.metallic;
                brdfInput.roughness = surfaceData.roughness;
                brdfInput.irradiance = 0;
                brdfInput.aoVisibility = 1.0f;
                brdfInput.bentNormalWS = surfaceData.normalWS;

                UNITY_UNROLLX(MAX_DIRECTIONAL_LIGHTS)
                for (uint lightIndex = 0; lightIndex < lightCount; ++lightIndex)
                {
                    const Light light = GetDirectionalLight(lightIndex, surfaceData.positionWS);
                    brdfInput.lightDirectionWS = light.direction;
                    brdfInput.lightColor = light.color;
                    brdfInput.shadowAttenuation = light.shadowAttenuation;
                    lighting += PBRLighting::ComputeLightingDirect(brdfInput);
                }

                const AAAAClusteredLightingGridCell lightGridCell = ClusteredLighting::LoadCell(surfaceData.positionWS, IN.texcoord);

                for (uint i = 0; i < lightGridCell.Count; ++i)
                {
                    const uint  punctualLightIndex = ClusteredLighting::LoadLightIndex(lightGridCell, i);
                    const Light light = GetPunctualLight(punctualLightIndex, surfaceData.positionWS);
                    brdfInput.lightDirectionWS = light.direction;
                    brdfInput.lightColor = light.color;
                    brdfInput.shadowAttenuation = 1.0f;
                    lighting += light.distanceAttenuation * PBRLighting::ComputeLightingDirect(brdfInput);
                }

                return float4(lighting, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Deferred Lighting: Indirect"


            HLSLPROGRAM
            #pragma vertex OverrideVert
            #pragma fragment Frag

            float4 Frag(const Varyings IN) : SV_Target
            {
                const float        deviceDepth = SampleDeviceDepth(IN.texcoord);
                const GBufferValue gbuffer = SampleGBuffer(IN.texcoord);
                SurfaceData        surfaceData;
                InitializeSurfaceData(gbuffer, IN, deviceDepth, surfaceData);

                const float3 lighting = PBRLighting::ComputeLightingIndirect(surfaceData);
                return float4(lighting, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}