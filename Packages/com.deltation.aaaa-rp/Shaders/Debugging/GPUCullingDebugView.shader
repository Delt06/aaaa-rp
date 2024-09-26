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
            #include "Packages/com.deltation.aaaa-rp/Runtime/Debugging/AAAADebugDisplaySettingsRendering.cs.hlsl"

            float _InstanceCountLimit;
            float _MeshletCountLimit;
            float _Mode;

            void GetCounts(const AAAAGPUCullingDebugData debugData, out uint instanceCount, out uint meshletCount)
            {
                switch (_Mode)
                {
                case AAAAGPUCULLINGDEBUGVIEWMODE_FRUSTUM:
                    instanceCount = debugData.FrustumCulledInstances;
                    meshletCount = debugData.FrustumCulledMeshlets;
                    break;
                case AAAAGPUCULLINGDEBUGVIEWMODE_OCCLUSION:
                    instanceCount = debugData.OcclusionCulledInstances;
                    meshletCount = debugData.OcclusionCulledMeshlets;
                    break;
                case AAAAGPUCULLINGDEBUGVIEWMODE_CONE:
                    instanceCount = 0;
                    meshletCount = debugData.ConeCulledMeshlets;
                    break;
                default:
                    instanceCount = 0;
                    meshletCount = 0;
                    break;
                }
            }

            float4 Frag(const Varyings IN) : SV_Target
            {
                const uint                    itemIndex = GPUCullingDebug::ScreenUVToBufferItemIndex(IN.texcoord);
                const AAAAGPUCullingDebugData debugData = _GPUCullingDebugDataBuffer[itemIndex];

                uint instanceCount, meshletCount;
                GetCounts(debugData, instanceCount, meshletCount);

                float3 mappedCounts;
                mappedCounts.x = instanceCount / _InstanceCountLimit;
                mappedCounts.y = meshletCount / _MeshletCountLimit;
                mappedCounts.z = 0;
                mappedCounts = saturate(mappedCounts);

                float3 result = lerp(0.25f, 0.75f, mappedCounts);
                result *= mappedCounts > 0;

                return float4(result, 1.0);
            }

            ENDHLSL
        }
    }

    Fallback Off
}