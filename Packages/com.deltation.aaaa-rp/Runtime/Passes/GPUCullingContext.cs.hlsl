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
    float4x4 ViewProjectionMatrix;
    float4x4 ViewMatrix;
    float4 CameraPosition;
    float4 FrustumPlanes[6];
    float4 CullingSphereLS;
    int PassMask;
    int CameraIsPerspective;
    uint BaseStartInstance;
    uint MeshletListBuildJobsOffset;
};

// Generated from DELTation.AAAARP.Passes.GPULODSelectionContext
// PackingRules = Exact
struct GPULODSelectionContext
{
    float4x4 ViewProjectionMatrix;
    float4 CameraPosition;
    float4 CameraUp;
    float4 CameraRight;
    float2 ScreenSizePixels;
};


#endif
