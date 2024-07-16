#ifndef AAAA_VISIBILITY_BUFFER_MESH_LOD_INCLUDED
#define AAAA_VISIBILITY_BUFFER_MESH_LOD_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.deltation.aaaa-rp/Runtime/AAAAStructs.cs.hlsl"

StructuredBuffer<AAAAMeshLOD> _MeshLODs;
float                         _MeshLODBias;
float                         _FullScreenMeshletBudget;

#define LOD_MASK (LOD_COUNT - 1u)

uint PackInstanceID_MeshLOD(const uint instanceID, const uint lod)
{
    return instanceID << LOD_BITS | lod;
}

void UnpackInstanceID_MeshLOD(const uint packedValue, out uint instanceID, out uint lod)
{
    instanceID = packedValue >> LOD_BITS;
    lod = packedValue & LOD_MASK;
}

AAAAMeshLOD PullMeshLODRaw(const uint meshLodStartIndex, const uint lodIndex)
{
    return _MeshLODs[meshLodStartIndex + lodIndex];
}

float SelectMeshLOD(const uint meshLodStartIndex, const float2 sizeSS)
{
    const float desiredMeshletCount = _FullScreenMeshletBudget * sizeSS.x * sizeSS.y;

    UNITY_LOOP
    for (uint lodIndex = 0; lodIndex < LOD_COUNT; ++lodIndex)
    {
        const AAAAMeshLOD meshLOD = PullMeshLODRaw(meshLodStartIndex, lodIndex);
        if ((float)meshLOD.MeshletCount < desiredMeshletCount)
        {
            return lodIndex;
        }
    }

    return LOD_COUNT - 1;
}

AAAAMeshLOD PullMeshLOD(const uint meshLodStartIndex, const float lod, out uint effectiveLod)
{
    effectiveLod = (uint)clamp(lod + _MeshLODBias, 0, LOD_COUNT - 1);
    return PullMeshLODRaw(meshLodStartIndex, effectiveLod);
}

#endif // AAAA_VISIBILITY_BUFFER_MESH_LOD_INCLUDED