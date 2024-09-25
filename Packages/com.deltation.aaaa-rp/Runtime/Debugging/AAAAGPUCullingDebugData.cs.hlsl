//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef AAAAGPUCULLINGDEBUGDATA_CS_HLSL
#define AAAAGPUCULLINGDEBUGDATA_CS_HLSL
//
// DELTation.AAAARP.Debugging.AAAAGPUCullingDebugGranularity:  static fields
//
#define AAAAGPUCULLINGDEBUGGRANULARITY_INSTANCE (0)
#define AAAAGPUCULLINGDEBUGGRANULARITY_MESHLET (1)

//
// DELTation.AAAARP.Debugging.AAAAGPUCullingDebugType:  static fields
//
#define AAAAGPUCULLINGDEBUGTYPE_FRUSTUM (0)
#define AAAAGPUCULLINGDEBUGTYPE_OCCLUSION (1)
#define AAAAGPUCULLINGDEBUGTYPE_CONE (2)

//
// DELTation.AAAARP.Debugging.AAAAGPUCullingDebugData:  static fields
//
#define GPUCULLING_DEBUG_BUFFER_DIMENSION (16)

// Generated from DELTation.AAAARP.Debugging.AAAAGPUCullingDebugData
// PackingRules = Exact
struct AAAAGPUCullingDebugData
{
    uint OcclusionCulledInstances;
    uint OcclusionCulledMeshlets;
};


#endif
