Shader "AAAA/Lit"
{
    Properties
    {
        [MainTexture]
        _BaseMap("Albedo", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "AAAAPipeline"
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
    }
}