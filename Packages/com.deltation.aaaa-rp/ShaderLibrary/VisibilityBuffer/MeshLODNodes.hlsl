#ifndef AAAA_VISIBILITY_BUFFER_MESH_LOD_NODES_INCLUDED
#define AAAA_VISIBILITY_BUFFER_MESH_LOD_NODES_INCLUDED

#include "Packages/com.deltation.aaaa-rp/Runtime/AAAAStructs.cs.hlsl"

StructuredBuffer<AAAAMeshLODNode> _MeshLODNodes;

AAAAMeshLODNode PullMeshLODNode(const uint nodeIndex)
{
    return _MeshLODNodes[nodeIndex];
}

#endif // AAAA_VISIBILITY_BUFFER_MESH_LOD_NODES_INCLUDED