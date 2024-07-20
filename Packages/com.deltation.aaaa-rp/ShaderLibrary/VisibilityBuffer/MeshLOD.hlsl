#ifndef AAAA_VISIBILITY_BUFFER_MESH_LOD_INCLUDED
#define AAAA_VISIBILITY_BUFFER_MESH_LOD_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.deltation.aaaa-rp/Runtime/AAAAStructs.cs.hlsl"

StructuredBuffer<AAAAMeshLODNode> _MeshLODNodes;
float                             _FullScreenMeshletBudget;

#define LOCAL_LOD_NODE_INDEX_BITS 8
#define LOCAL_LOD_NODE_INDEX_MASK ((1u << LOCAL_LOD_NODE_INDEX_BITS) - 1u)

uint PackInstanceID_LocalNodeIndex(const uint instanceID, const uint localNodeIndex)
{
    return instanceID << LOCAL_LOD_NODE_INDEX_BITS | localNodeIndex;
}

void UnpackInstanceID_LocalNodeIndex(const uint packedValue, out uint instanceID, out uint localNodeIndex)
{
    instanceID = packedValue >> LOCAL_LOD_NODE_INDEX_BITS;
    localNodeIndex = packedValue & LOCAL_LOD_NODE_INDEX_MASK;
}

AAAAMeshLODNode PullMeshLODNodeRaw(const uint meshLodNodeIndex)
{
    return _MeshLODNodes[meshLodNodeIndex];
}

#endif // AAAA_VISIBILITY_BUFFER_MESH_LOD_INCLUDED