Shader "Hidden/AAAA/PostProcessing/Uber"
{
    HLSLINCLUDE
    #pragma target 2.0
    #pragma editor_sync_compilation

    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/PostProcessing/ToneMapping.hlsl"
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "AAAAPipeline"
        }

        Pass
        {
            Name "Uber Post"

            ZWrite Off
            ZTest Off
            ZClip Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_local _ _TONEMAP_NEUTRAL _TONEMAP_ACES

            void ToneMap(inout float3 color)
            {
                #if defined(_TONEMAP_NEUTRAL)
                color = ToneMapping::Neutral(color);
                #elif defined(_TONEMAP_ACES)
                color = ToneMapping::ACES(color);
                #endif
            }

            float4 Frag(const Varyings IN) : SV_Target
            {
                const float4 source = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, IN.texcoord.xy, 0);
                float3       result = source;

                ToneMap(result);

                return float4(result, source.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}