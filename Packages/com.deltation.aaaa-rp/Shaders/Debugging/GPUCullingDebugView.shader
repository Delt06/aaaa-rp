Shader "Hidden/AAAA/GPUCullingDebugView"
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
        Tags
        {
            "RenderPipeline" = "AAAAPipeline"
        }

        Pass
        {
            ZWrite Off
            ZTest Off
            ZClip Off
            Cull Off

            Blend One One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #define GPU_CULLING_DEBUG_DATA_BUFFER_READONLY
            #define DEBUG_GPU_CULLING
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Debug/GPUCullingDebug.hlsl"

            float _InstanceCountLimit;
            float _MeshletCountLimit;

            float4 Frag(const Varyings IN) : SV_Target
            {
                const uint                    itemIndex = GPUCullingDebug::ScreenUVToBufferItemIndex(IN.texcoord);
                const AAAAGPUCullingDebugData debugData = _GPUCullingDebugDataBuffer[itemIndex];

                float3 mappedCounts;
                mappedCounts.x = debugData.OcclusionCulledInstances / _InstanceCountLimit;
                mappedCounts.y = debugData.OcclusionCulledMeshlets / _MeshletCountLimit;
                mappedCounts.z = 0;
                mappedCounts = saturate(mappedCounts);

                float3 result = lerp(0.25, 0.75f, mappedCounts);
                result *= mappedCounts > 0;

                return float4(result, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}