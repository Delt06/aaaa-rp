Shader "Hidden/AAAA/VXGI/ConeTrace"
{
    Properties {}

    HLSLINCLUDE
    #pragma target 5.0
    #pragma editor_sync_compilation

    #include_with_pragmas "Packages/com.deltation.aaaa-rp/ShaderLibrary/Bindless.hlsl"
    #include_with_pragmas "Packages/com.deltation.aaaa-rp/Shaders/GlobalIllumination/XeGTAO/GTAOPragma.hlsl"
    #include_with_pragmas "Packages/com.deltation.aaaa-rp/Shaders/GlobalIllumination/VXGI/Variants.hlsl"

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
        surfaceData.positionCS = IN.positionCS;

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
            Name "VXGI Cone Trace: Diffuse"

            HLSLPROGRAM
            #pragma vertex OverrideVert
            #pragma fragment Frag

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

                return VXGI::Tracing::ConeTraceDiffuse(surfaceData.positionWS, surfaceData.normalWS);
            }
            ENDHLSL
        }
    }

    Fallback Off
}