//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef AAAACLUSTEREDLIGHTINGSHADERDATA_CS_HLSL
#define AAAACLUSTEREDLIGHTINGSHADERDATA_CS_HLSL
//
// DELTation.AAAARP.Passes.ClusteredLighting.AAAAClusteredLightingComputeShaders:  static fields
//
#define BUILD_CLUSTER_GRID_THREAD_GROUP_SIZE (32)
#define CLUSTER_CULLING_THREAD_GROUP_SIZE (32)

//
// DELTation.AAAARP.Passes.ClusteredLighting.AAAAClusteredLightingConstantBuffer:  static fields
//
#define CLUSTERS_X (16)
#define CLUSTERS_Y (9)
#define CLUSTERS_Z (24)
#define TOTAL_CLUSTERS (3456)

// Generated from DELTation.AAAARP.Passes.ClusteredLighting.AAAAClusterBounds
// PackingRules = Exact
struct AAAAClusterBounds
{
    float4 Min;
    float4 Max;
};


#endif
