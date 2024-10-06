Shader "Hidden/AAAA/AntiAliasing/SMAA"
{
    Properties
    {
        [HideInInspector] _StencilRef ("_StencilRef", Int) = 64
        [HideInInspector] _StencilMask ("_StencilMask", Int) = 64
    }
    HLSLINCLUDE
    #pragma target 5.0
    #pragma editor_sync_compilation

    #pragma multi_compile_local SMAA_PRESET_LOW SMAA_PRESET_MEDIUM SMAA_PRESET_HIGH SMAA_PRESET_ULTRA
    
    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/GlobalSamplers.hlsl"

    #define SMAA_RT_METRICS (_BlitTexture_TexelSize)
    #define SMAA_AREATEX_SELECT(s) s.rg
    #define SMAA_SEARCHTEX_SELECT(s) s.a
    #define SMAA_HLSL_4_1
    #define LinearSampler sampler_LinearClamp
    #define PointSampler sampler_PointClamp
    
    #if UNITY_COLORSPACE_GAMMA
    #define GAMMA_FOR_EDGE_DETECTION (1)
    #else
    #define GAMMA_FOR_EDGE_DETECTION (1/2.2)
    #endif
    
    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/ThirdParty/SMAA/SMAA.hlsl"

    // Non-temporal mode
    #define SUBSAMPLE_INDICES float4(0, 0, 0, 0)
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "AAAAPipeline"
        }

        ZWrite Off ZTest Always Blend Off Cull Off

        Pass
        {
            Name "SMAA: Edge Detection"

            Stencil
            {
                WriteMask [_StencilMask]
                Ref [_StencilRef]
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex NeighborhoodBlendingVert
            #pragma fragment Frag

            struct NeighborhoodBlendingVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                float4 offset[3] : TEXCOORD1;
            };

            NeighborhoodBlendingVaryings NeighborhoodBlendingVert(Attributes input)
            {
                NeighborhoodBlendingVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);

                const Varyings defaultOutput = Vert(input);

                output.positionCS = defaultOutput.positionCS;
                output.texcoord = defaultOutput.texcoord;
                SMAAEdgeDetectionVS(defaultOutput.texcoord, output.offset);

                return output;
            }

            float4 Frag(NeighborhoodBlendingVaryings input) : SV_Target
            {
                return float4(SMAAColorEdgeDetectionPS(input.texcoord, input.offset, _BlitTexture), 0, 0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "SMAA: Blending Weights Calculation"

            Stencil
            {
                WriteMask [_StencilMask]
                ReadMask [_StencilMask]
                Ref [_StencilRef]
                Comp Equal
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex NeighborhoodBlendingVert
            #pragma fragment Frag

            TEXTURE2D(_EdgesTex);
            TEXTURE2D(_AreaTex);
            TEXTURE2D(_SearchTex);

            struct NeighborhoodBlendingVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                float2 pixcoord : TEXCOORD1;
                float4 offset[3] : TEXCOORD2;
            };

            NeighborhoodBlendingVaryings NeighborhoodBlendingVert(Attributes input)
            {
                NeighborhoodBlendingVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);

                const Varyings defaultOutput = Vert(input);

                output.positionCS = defaultOutput.positionCS;
                output.texcoord = defaultOutput.texcoord;
                SMAABlendingWeightCalculationVS(defaultOutput.texcoord, output.pixcoord, output.offset);

                return output;
            }

            float4 Frag(NeighborhoodBlendingVaryings input) : SV_Target
            {
                return SMAABlendingWeightCalculationPS(input.texcoord, input.pixcoord, input.offset, _EdgesTex, _AreaTex, _SearchTex, SUBSAMPLE_INDICES);
            }
            ENDHLSL
        }

        Pass
        {
            Name "SMAA: Neighborhood Bledning"

            HLSLPROGRAM
            #pragma vertex NeighborhoodBlendingVert
            #pragma fragment Frag

            TEXTURE2D(_BlendTex);

            struct NeighborhoodBlendingVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                float4 offset : TEXCOORD2;
            };

            NeighborhoodBlendingVaryings NeighborhoodBlendingVert(Attributes input)
            {
                NeighborhoodBlendingVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);

                const Varyings defaultOutput = Vert(input);

                output.positionCS = defaultOutput.positionCS;
                output.texcoord = defaultOutput.texcoord;
                SMAANeighborhoodBlendingVS(defaultOutput.texcoord, output.offset);

                return output;
            }

            float4 Frag(NeighborhoodBlendingVaryings input) : SV_Target
            {
                return SMAANeighborhoodBlendingPS(input.texcoord, input.offset, _BlitTexture, _BlendTex);
            }
            ENDHLSL
        }
    }

    Fallback Off
}