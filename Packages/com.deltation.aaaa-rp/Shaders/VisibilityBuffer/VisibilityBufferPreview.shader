Shader "Hidden/AAAA/VisibilityBufferPreview"
{
    HLSLINCLUDE

        #pragma target 2.0
        #pragma editor_sync_compilation
        #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Debug/DebuggingFullscreen.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "AAAAPipeline" }

        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment Frag

                #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Utils.hlsl"

                #define DIM 0.15
                #define BRIGHT 0.8
                #define COLORS_COUNT 6

                static float3 colors[COLORS_COUNT] = {
                    float3(DIM, BRIGHT, DIM),
                    float3(BRIGHT, BRIGHT, DIM),
                    float3(BRIGHT, DIM, DIM),
                    float3(DIM, BRIGHT, BRIGHT),
                    float3(BRIGHT, DIM, BRIGHT),
                    float3(DIM, DIM, BRIGHT),
                };

                // #define DISPLAY_INSTANCE_ID
                #define DISPLAY_MESHLET_ID
                // #define DISPLAY_INDEX_ID

                uint DisplayedValue(const uint2 visibilityValue)
                {
                    uint instanceID, meshletID, indexID; 
                    UnpackVisibilityBufferValue(visibilityValue, instanceID, meshletID, indexID);
#if defined(DISPLAY_INSTANCE_ID)
                    return instanceID;
#elif defined(DISPLAY_MESHLET_ID)
                    return meshletID;
#elif defined(DISPLAY_INDEX_ID)
                    return indexID;
#else
                    return 0;
#endif
                }

                float4 Frag(const Varyings IN) : SV_Target
                {
                    const uint2 visibilityValue = asuint(FragBlit(IN, sampler_PointClamp).xy);
                    const uint displayedValue = DisplayedValue(visibilityValue);
                    const float3 displayColor = colors[displayedValue % COLORS_COUNT];
                    return float4(displayColor, 1);
                }
            ENDHLSL
        }
    }

    Fallback Off
}
