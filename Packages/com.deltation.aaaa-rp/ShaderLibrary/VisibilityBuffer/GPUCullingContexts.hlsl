#ifndef AAAA_VISIBILITY_BUFFER_GPU_CULLING_CONTEXTS_INCLUDED
#define AAAA_VISIBILITY_BUFFER_GPU_CULLING_CONTEXTS_INCLUDED

#include "Packages/com.deltation.aaaa-rp/Runtime/Passes/GPUCullingContext.cs.hlsl"

struct GPUCullingContextArray
{
    GPUCullingContext Items[MAX_CULLING_CONTEXTS_PER_BATCH];
};

struct GPULODSelectionContextArray
{
    GPULODSelectionContext Items[MAX_CULLING_CONTEXTS_PER_BATCH];
};

#endif // AAAA_VISIBILITY_BUFFER_GPU_CULLING_CONTEXTS_INCLUDED