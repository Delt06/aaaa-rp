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

            #include_with_pragmas "Packages/com.deltation.aaaa-rp/ShaderLibrary/Bindless.hlsl"

            #pragma multi_compile_local _ _ALPHATEST_ON

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

            #include_with_pragmas "Packages/com.deltation.aaaa-rp/ShaderLibrary/Bindless.hlsl"
            #pragma enable_d3d11_debug_symbols

            #pragma multi_compile_local _ _ALPHATEST_ON

            #include "Packages/com.deltation.aaaa-rp/Shaders/VisibilityBuffer/VisibilityBufferShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}