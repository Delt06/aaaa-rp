#ifndef AAAA_VISIBILITY_BUFFER_MESHLETS_INCLUDED
#define AAAA_VISIBILITY_BUFFER_MESHLETS_INCLUDED

#include "Packages/com.deltation.aaaa-rp/Runtime/AAAAStructs.cs.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/MeshLOD.hlsl"

uint                                _MeshletCount;
StructuredBuffer<AAAAMeshlet>       _Meshlets;
StructuredBuffer<AAAAMeshletVertex> _SharedVertexBuffer;
ByteAddressBuffer                   _SharedIndexBuffer;

uint MeshletRenderRequestIndexToAddress(const uint index)
{
    const uint uintSize = 4;
    return index * uintSize * 2;
}

struct AAAAMeshletRenderRequest
{
    uint InstanceID;
    uint LocalNodeIndex;
    uint MeshletID;
};

AAAAMeshletRenderRequest PullMeshletRenderRequest(ByteAddressBuffer renderRequests, const uint index)
{
    const uint  address = MeshletRenderRequestIndexToAddress(index);
    const uint2 value = renderRequests.Load2(address);

    AAAAMeshletRenderRequest renderRequest;
    UnpackInstanceID_LocalNodeIndex(value.x, renderRequest.InstanceID, renderRequest.LocalNodeIndex);
    renderRequest.MeshletID = value.y;
    return renderRequest;
}

void StoreMeshletRenderRequest(RWByteAddressBuffer renderRequests, const uint index, AAAAMeshletRenderRequest renderRequest)
{
    const uint  address = MeshletRenderRequestIndexToAddress(index);
    const uint2 value = uint2(PackInstanceID_LocalNodeIndex(renderRequest.InstanceID, renderRequest.LocalNodeIndex), renderRequest.MeshletID);
    renderRequests.Store2(address, value);
}

AAAAMeshlet PullMeshletData(const uint meshletID)
{
    return _Meshlets[meshletID];
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

#endif // AAAA_VISIBILITY_BUFFER_MESHLETS_INCLUDED