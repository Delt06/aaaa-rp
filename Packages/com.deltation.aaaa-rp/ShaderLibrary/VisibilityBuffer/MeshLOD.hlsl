#ifndef AAAA_VISIBILITY_BUFFER_MESH_LOD_INCLUDED
#define AAAA_VISIBILITY_BUFFER_MESH_LOD_INCLUDED

#include "Packages/com.deltation.aaaa-rp/Runtime/AAAAStructs.cs.hlsl"

StructuredBuffer<AAAAMeshLOD> _MeshLODs;
int                           _MeshLODBias;

AAAAMeshLOD PullMeshLOD(const uint meshLodStartIndex, const uint lod)
{
    const uint effectiveLod = clamp((int)lod + _MeshLODBias, 0, LOD_COUNT - 1);
    return _MeshLODs[meshLodStartIndex + effectiveLod];
}

#endif // AAAA_VISIBILITY_BUFFER_MESH_LOD_INCLUDED