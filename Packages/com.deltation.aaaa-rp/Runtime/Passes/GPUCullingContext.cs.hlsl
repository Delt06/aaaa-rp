//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef GPUCULLINGCONTEXT_CS_HLSL
#define GPUCULLINGCONTEXT_CS_HLSL
//
// DELTation.AAAARP.Passes.GPUCullingContext:  static fields
//
#define MAX_CULLING_CONTEXTS_PER_BATCH (8)

// Generated from DELTation.AAAARP.Passes.GPUCullingContext
// PackingRules = Exact
struct GPUCullingContext
{
    float4x4 CullingViewProjection;
    float4x4 CullingView;
    float4 CullingCameraPosition;
    float4 CullingFrustumPlanes[6];
    float4 CullingSphereLS;
    int CullingPassMask;
    int CullingCameraIsPerspective;
};

// Generated from DELTation.AAAARP.Passes.GPULODSelectionContext
// PackingRules = Exact
struct GPULODSelectionContext
{
    float4x4 LODCameraViewProjection;
    float4 LODCameraPosition;
    float4 LODCameraUp;
    float4 LODCameraRight;
    float2 LODScreenSizePixels;
};


#endif
