#ifndef AAAA_GPU_CULLING_DEBUG_INCLUDED
#define AAAA_GPU_CULLING_DEBUG_INCLUDED

#include "Packages/com.deltation.aaaa-rp/Runtime/Debugging/AAAAGPUCullingDebugData.cs.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Math.hlsl"

#ifdef DEBUG_GPU_CULLING
#ifdef GPU_CULLING_DEBUG_DATA_BUFFER_READONLY
StructuredBuffer
#else
RWStructuredBuffer
#endif
<AAAAGPUCullingDebugData> _GPUCullingDebugDataBuffer;
#endif

struct GPUCullingDebug
{
    static uint ScreenUVToBufferItemIndex(const float2 uv)
    {
        const uint2 cellIndices = (uint2)(clamp(uv, 0, 0.999f) * GPUCULLING_DEBUG_BUFFER_DIMENSION);
        return cellIndices.x * GPUCULLING_DEBUG_BUFFER_DIMENSION + cellIndices.y;
    }

    #ifndef GPU_CULLING_DEBUG_DATA_BUFFER_READONLY
    static void OnCulled(const float2 screenUV, const uint cullingGranularity, const uint cullingType)
    {
        #ifdef DEBUG_GPU_CULLING
        const uint itemIndex = ScreenUVToBufferItemIndex(screenUV);

        switch (cullingType)
        {
        case AAAAGPUCULLINGDEBUGTYPE_FRUSTUM:
            switch (cullingGranularity)
            {
            case AAAAGPUCULLINGDEBUGGRANULARITY_INSTANCE:
                InterlockedAdd(_GPUCullingDebugDataBuffer[itemIndex].FrustumCulledInstances, 1);
                break;
            case AAAAGPUCULLINGDEBUGGRANULARITY_MESHLET:
                InterlockedAdd(_GPUCullingDebugDataBuffer[itemIndex].FrustumCulledMeshlets, 1);
                break;
            default:
                break;
            }
            break;
        case AAAAGPUCULLINGDEBUGTYPE_OCCLUSION:
            switch (cullingGranularity)
            {
            case AAAAGPUCULLINGDEBUGGRANULARITY_INSTANCE:
                InterlockedAdd(_GPUCullingDebugDataBuffer[itemIndex].OcclusionCulledInstances, 1);
                break;
            case AAAAGPUCULLINGDEBUGGRANULARITY_MESHLET:
                InterlockedAdd(_GPUCullingDebugDataBuffer[itemIndex].OcclusionCulledMeshlets, 1);
                break;
            default:
                break;
            }
            break;
        case AAAAGPUCULLINGDEBUGTYPE_CONE:
            switch (cullingGranularity)
            {
            case AAAAGPUCULLINGDEBUGGRANULARITY_INSTANCE:
                break;
            case AAAAGPUCULLINGDEBUGGRANULARITY_MESHLET:
                InterlockedAdd(_GPUCullingDebugDataBuffer[itemIndex].ConeCulledMeshlets, 1);
                break;
            default:
                break;
            }
            break;
        default:
            break;
        }
        #endif
    }

    static void OnCulled(const BoundingSquareSS boundingSquareSS, const uint cullingGranularity, const uint cullingType)
    {
        OnCulled((boundingSquareSS.MinUV + boundingSquareSS.MaxUV) * 0.5f, cullingGranularity, cullingType);
    }
    #endif
};

#endif // AAAA_GPU_CULLING_DEBUG_INCLUDED