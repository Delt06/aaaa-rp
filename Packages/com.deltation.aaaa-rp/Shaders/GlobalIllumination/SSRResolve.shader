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
                const float4 traceValue = SAMPLE_TEXTURE2D_LOD(_SSRTraceResult, sampler_LinearClamp, IN.texcoord, 0);
                if (all(traceValue == 0))
                {
                    return 0;
                }

                const float3 reflection = SAMPLE_TEXTURE2D_LOD(_CameraColor, sampler_LinearClamp, traceValue.xy, 0).rgb;
                return float4(reflection, traceValue.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "SSR Resolve: Bilateral Blur"


            HLSLPROGRAM
            #pragma vertex OverrideVert
            #pragma fragment Frag

            TEXTURE2D(_SSRTraceResult);
            TEXTURE2D(_SSRResolveResult);
            float4 _BlurVectorRange;

            float4 Frag(const Varyings IN) : SV_Target
            {
                const float4 traceValue = SAMPLE_TEXTURE2D_LOD(_SSRTraceResult, sampler_LinearClamp, IN.texcoord, 0);
                const float  blurRadius = lerp(_BlurVectorRange.z, _BlurVectorRange.w, traceValue.z);

                static const int taps = 6;
                const float      tapOffsets[taps] =
                {
                    -2.5f, -1.5f, -0.5f,
                    0.5f, 1.5f, 2.5f,
                };

                float4 result = 0;

                for (int i = 0; i < taps; ++i)
                {
                    const float2 uv = IN.texcoord + blurRadius * tapOffsets[i] * _BlurVectorRange.xy;
                    result += SAMPLE_TEXTURE2D_LOD(_SSRResolveResult, sampler_LinearClamp, uv, 0);

                }

                return result / taps;
            }
            ENDHLSL
        }

        Pass
        {
            Name "SSR Resolve: Compose"

            ZTest Greater
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex OverrideVert
            #pragma fragment Frag


            float4 Frag(const Varyings IN) : SV_Target
            {
                const float4 ssrValue = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, IN.texcoord, 0);
                const float  roughnessAttenuation = 1;

                return float4(ssrValue.rgb, ssrValue.a * roughnessAttenuation);
            }
            ENDHLSL
        }
    }
}