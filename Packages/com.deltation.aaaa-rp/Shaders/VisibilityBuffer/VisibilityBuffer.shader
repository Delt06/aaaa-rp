Shader "Hidden/AAAA/VisibilityBuffer"
{
    Properties {}
    SubShader
    {
        ZTest Less
        ZWrite On

        Pass
        {
            Name "Visibility Buffer"
            Tags
            {
                "LightMode" = "Visibility"
            }

            HLSLPROGRAM
            #pragma vertex VS
            #pragma fragment PS

            #include "Packages/com.deltation.aaaa-rp/Shaders/VisibilityBuffer/VisibilityBufferPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            ZClip Off

            Name "Visibility Buffer: Shadow Caster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            HLSLPROGRAM
            #pragma vertex ShadowCasterVS
            #pragma fragment ShadowCasterPS

            #include "Packages/com.deltation.aaaa-rp/Shaders/VisibilityBuffer/VisibilityBufferShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}