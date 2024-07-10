#ifndef AAAA_VISIBILITY_BUFFER_MESHLETS_INCLUDED
#define AAAA_VISIBILITY_BUFFER_MESHLETS_INCLUDED

#include "Packages/com.deltation.aaaa-rp/Runtime/Meshlets/AAAAMeshletCollection.cs.hlsl"

uint                                  _MeshletCount;
StructuredBuffer<AAAAMeshlet>         _Meshlets;
StructuredBuffer<AAAAMeshletVertex>   _SharedVertexBuffer;
ByteAddressBuffer                     _SharedIndexBuffer;
StructuredBuffer<AAAAPerInstanceData> _PerInstanceData;

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

#endif // AAAA_VISIBILITY_BUFFER_MESHLETS_INCLUDED