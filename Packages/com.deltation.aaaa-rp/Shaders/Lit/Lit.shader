Shader "AAAA/Lit"
{
    Properties
    {
        [MainColor] _BaseColor("Tint", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}

        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 0
        _AlphaClipThreshold("Alpha Clip Threshold", Range(0, 1)) = 0.5

        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)

        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpMapScale("Normals Strength", Range(0, 10)) = 1

        [Enum(UnityEngine.Rendering.CullMode)] _CullMode("Cull Mode", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "AAAAPipeline" "RenderType" = "Opaque"
        }

        Pass
        {
            Name "Lit GBuffer"

            ZTest LEqual
            ZWrite On
            Cull [_CullMode]

            Tags
            {
                "LightMode" = "GBuffer"
            }

            HLSLPROGRAM
            #pragma vertex VS
            #pragma fragment PS

            #pragma shader_feature _ALPHATEST_ON

            #include "Packages/com.deltation.aaaa-rp/Shaders/Lit/LitGBufferPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Lit Meta"

            Cull Off

            Tags
            {
                "LightMode" = "Meta"
            }

            HLSLPROGRAM
            #pragma vertex VS
            #pragma fragment PS

            #pragma shader_feature _ALPHATEST_ON

            #pragma shader_feature EDITOR_VISUALIZATION

            #include "Packages/com.deltation.aaaa-rp/Shaders/Lit/LitMetaPass.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "DELTation.AAAARP.Editor.Shaders.AAAAShaderGUI"
}