Shader "Hidden/AAAA/SSR/Resolve"
{
    Properties {}

    HLSLINCLUDE
    #pragma target 5.0
    #pragma editor_sync_compilation

    #pragma enable_d3d11_debug_symbols

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

                const float3 reflection = SAMPLE_TEXTURE2D(_CameraColor, sampler_LinearClamp, traceValue).rgb;
                return float4(reflection * traceValue.a, traceValue.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "SSR Resolve: Compose"

            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex OverrideVert
            #pragma fragment Frag

            TEXTURE2D(_SSRResolveResult);

            float4 Frag(const Varyings IN) : SV_Target
            {
                float4 ssrValue = SAMPLE_TEXTURE2D(_SSRResolveResult, sampler_LinearClamp, IN.texcoord);
                return float4(ssrValue.rgb, ssrValue.a);
            }
            ENDHLSL
        }
    }
}