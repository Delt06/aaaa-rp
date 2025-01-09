Shader "Hidden/AAAA/VXGI/ConeTrace"
{
    Properties {}

    HLSLINCLUDE
    #pragma target 5.0
    #pragma editor_sync_compilation

    #include_with_pragmas "Packages/com.deltation.aaaa-rp/ShaderLibrary/Bindless.hlsl"
    #include_with_pragmas "Packages/com.deltation.aaaa-rp/Shaders/GlobalIllumination/VXGI/Variants.hlsl"
    #include_with_pragmas "Packages/com.deltation.aaaa-rp/Shaders/GlobalIllumination/XeGTAO/GTAOPragma.hlsl"

    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    #include "Packages/com.deltation.aaaa-rp/Runtime/AAAAStructs.cs.hlsl"
    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/CameraDepth.hlsl"
    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/GBuffer.hlsl"
    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/PBR.hlsl"

    Varyings OverrideVert(Attributes input)
    {
        Varyings output = Vert(input);

        output.positionCS.z = UNITY_RAW_FAR_CLIP_VALUE * output.positionCS.w;

        return output;
    }

    struct MinSurfaceData
    {
        float3 positionWS;
        float3 normalWS;
        float  roughness;
        uint   materialFlags;
    };

    float3 ComputeWorldSpacePosition(const float2 screenUV, const float deviceDepth)
    {
        return ComputeWorldSpacePosition(screenUV, deviceDepth, UNITY_MATRIX_I_VP);
    }

    MinSurfaceData FetchSurfaceData(const float2 screenUV)
    {
        MinSurfaceData result;

        #ifdef GATHER
        const GBufferValue gbuffer = SampleGBufferLinear(screenUV);
        #else
        const GBufferValue gbuffer = SampleGBuffer(screenUV);
        #endif
        result.normalWS = gbuffer.normalWS;
        result.roughness = gbuffer.roughness;
        result.materialFlags = gbuffer.materialFlags;

        #ifdef GATHER
        const float4 deviceDepths = GatherDeviceDepth(screenUV);
        result.positionWS = 0.25f * (
            ComputeWorldSpacePosition(screenUV, deviceDepths[0]) + ComputeWorldSpacePosition(screenUV, deviceDepths[1]) +
            ComputeWorldSpacePosition(screenUV, deviceDepths[2]) + ComputeWorldSpacePosition(screenUV, deviceDepths[3]));
        #else
        const float deviceDepth = SampleDeviceDepth(screenUV);
        result.positionWS = ComputeWorldSpacePosition(screenUV, deviceDepth);
        #endif

        const float3 normalWS = result.normalWS;
        float aoVisibility;
        SampleGTAO(screenUV * _ScreenSize.xy, normalWS, aoVisibility, result.normalWS);

        return result;
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

            #pragma multi_compile_fragment _ GATHER

            float4 Frag(const Varyings IN) : SV_Target
            {
                const MinSurfaceData surfaceData = FetchSurfaceData(IN.texcoord);

                if (surfaceData.materialFlags & AAAAMATERIALFLAGS_UNLIT)
                {
                    return 0;
                }

                return VXGI::Tracing::ConeTraceDiffuse(surfaceData.positionWS, surfaceData.normalWS);
            }
            ENDHLSL
        }

        Pass
        {
            Name "VXGI Cone Trace: Specular"

            HLSLPROGRAM
            #pragma vertex OverrideVert
            #pragma fragment Frag

            #pragma multi_compile_fragment _ GATHER

            float4 Frag(const Varyings IN) : SV_Target
            {
                const MinSurfaceData surfaceData = FetchSurfaceData(IN.texcoord);

                if (surfaceData.materialFlags & AAAAMATERIALFLAGS_UNLIT)
                {
                    return 0;
                }

                return VXGI::Tracing::ConeTraceSpecular(surfaceData.positionWS, surfaceData.normalWS, surfaceData.roughness);
            }
            ENDHLSL
        }

        Pass
        {
            Name "VXGI Cone Trace: Specular, Compose"

            ZTest Greater
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex OverrideVert
            #pragma fragment Frag

            float4 Frag(const Varyings IN) : SV_Target
            {
                return VXGI::Tracing::LoadIndirectSpecular(IN.texcoord);
            }
            ENDHLSL
        }
    }

    Fallback Off
}