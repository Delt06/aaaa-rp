#ifndef AAAA_VISIBILITY_BUFFER_UTILS_INCLUDED
#define AAAA_VISIBILITY_BUFFER_UTILS_INCLUDED

#include "Packages/com.deltation.aaaa-rp/Runtime/Meshlets/AAAAMeshletCollection.cs.hlsl"

uint                                  _MeshletCount;
StructuredBuffer<AAAAMeshlet>         _Meshlets;
StructuredBuffer<AAAAMeshletVertex>   _SharedVertexBuffer;
ByteAddressBuffer                     _SharedIndexBuffer;
StructuredBuffer<AAAAPerInstanceData> _PerInstanceData;

#define VISIBILITY_BUFFER_INDEX_ID_BITS (8u)
#define VISIBILITY_BUFFER_INDEX_ID_MASK ((1u << VISIBILITY_BUFFER_INDEX_ID_BITS) - 1u)

uint2 PackVisibilityBufferValue(const uint instanceID, const uint meshletID, const uint indexID)
{
    return uint2(instanceID, meshletID << VISIBILITY_BUFFER_INDEX_ID_BITS | (indexID / 3) & VISIBILITY_BUFFER_INDEX_ID_MASK);
}

void UnpackVisibilityBufferValue(uint2 value, out uint instanceID, out uint meshletID, out uint indexID)
{
    instanceID = value.x;
    meshletID = value.y >> VISIBILITY_BUFFER_INDEX_ID_BITS;
    indexID = (value.y & VISIBILITY_BUFFER_INDEX_ID_MASK) * 3;
}

uint PullIndex(const AAAAMeshlet meshlet, const uint indexID)
{
    const uint absoluteIndexID = meshlet.TriangleOffset + indexID;
    const uint indices = _SharedIndexBuffer.Load(absoluteIndexID / 4 * 4);
    const uint shiftAmount = absoluteIndexID % 4 * 8;
    const uint mask = 0xFFu << shiftAmount;
    return (indices & mask) >> shiftAmount;
}

AAAAMeshletVertex PullVertex(const AAAAMeshlet meshlet, const uint index)
{
    return _SharedVertexBuffer[meshlet.VertexOffset + index];
}

#endif // AAAA_VISIBILITY_BUFFER_UTILS_INCLUDED