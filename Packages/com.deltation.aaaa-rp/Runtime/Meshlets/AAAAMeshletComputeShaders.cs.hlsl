//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef AAAAMESHLETCOMPUTESHADERS_CS_HLSL
#define AAAAMESHLETCOMPUTESHADERS_CS_HLSL
//
// DELTation.AAAARP.Meshlets.AAAAMeshletComputeShaders:  static fields
//
#define MAX_MESH_LODNODES_PER_INSTANCE (4096)
#define MESHLET_LIST_BUILD_INIT_THREAD_GROUP_SIZE (32)
#define MESHLET_LIST_BUILD_THREAD_GROUP_SIZE (32)
#define MESHLET_LIST_BUILD_SYNC_THREAD_GROUP_SIZE (1)
#define FIXUP_GPUMESHLET_CULLING_INDIRECT_DISPATCH_ARGS_THREAD_GROUP_SIZE (1)
#define GPUMESHLET_CULLING_THREAD_GROUP_SIZE (32)
#define FIXUP_MESHLET_INDIRECT_DRAW_ARGS_THREAD_GROUP_SIZE (1)

//
// DELTation.AAAARP.Meshlets.AAAAMeshletListBuildJob:  static fields
//
#define MAX_LODNODES_PER_THREAD_GROUP (128)

// Generated from DELTation.AAAARP.Meshlets.AAAAMeshletListBuildJob
// PackingRules = Exact
struct AAAAMeshletListBuildJob
{
    uint InstanceID;
    uint MeshLODNodeOffset;
    uint MeshLODNodeCount;
    uint Padding0;
};

//
// Accessors for DELTation.AAAARP.Meshlets.AAAAMeshletListBuildJob
//
uint GetInstanceID(AAAAMeshletListBuildJob value)
{
    return value.InstanceID;
}
uint GetMeshLODNodeOffset(AAAAMeshletListBuildJob value)
{
    return value.MeshLODNodeOffset;
}
uint GetMeshLODNodeCount(AAAAMeshletListBuildJob value)
{
    return value.MeshLODNodeCount;
}
uint GetPadding0(AAAAMeshletListBuildJob value)
{
    return value.Padding0;
}

#endif
