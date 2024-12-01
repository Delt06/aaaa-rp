Shader "AAAA/Lit"
{
    Properties
    {
        [MainColor] _BaseColor("Tint", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)
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
            Cull Off

            Tags
            {
                "LightMode" = "GBuffer"
            }

            HLSLPROGRAM
            #pragma vertex VS
            #pragma fragment PS

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

            #pragma shader_feature EDITOR_VISUALIZATION

            #include "Packages/com.deltation.aaaa-rp/Shaders/Lit/LitMetaPass.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "DELTation.AAAARP.Editor.Shaders.AAAAShaderGUI"
}