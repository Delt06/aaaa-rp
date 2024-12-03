Shader "Hidden/AAAA/LPV/Trace"
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
            Name "LPV Trace"

            HLSLPROGRAM
            #pragma vertex OverrideVert
            #pragma fragment Frag

            TEXTURE2D(_SSRTraceResult);
            TEXTURE2D(_CameraColor);

            float4 Frag(const Varyings IN) : SV_Target
            {
                return 0.5;
            }
            ENDHLSL
        }
    }
}