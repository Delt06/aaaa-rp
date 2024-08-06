#ifndef AAAA_MESHLET_LIST_BUILD_INCLUDED
#define AAAA_MESHLET_LIST_BUILD_INCLUDED

#define VISITED_MASK_BITS (32u)
#define WORK_NODE_STRIDE (16u)

struct WorkNode
{
    uint InstanceID;
    uint MeshLODNodeIndex;
    uint VisitedMaskOffset;
};

WorkNode UnpackWorkNode(uint3 value)
{
    WorkNode node;
    node.InstanceID = value.x;
    node.MeshLODNodeIndex = value.y;
    node.VisitedMaskOffset = value.z;
    return node;
}

uint3 PackWorkNode(WorkNode node)
{
    return uint3(node.InstanceID, node.MeshLODNodeIndex, node.VisitedMaskOffset);
}

WorkNode LoadWorkNode(ByteAddressBuffer buffer, const uint index)
{
    return UnpackWorkNode(buffer.Load3(index * WORK_NODE_STRIDE));
}

void StoreWorkNode(RWByteAddressBuffer buffer, const uint index, const uint3 packedNode)
{
    buffer.Store3(index * WORK_NODE_STRIDE, packedNode);
}

#endif // AAAA_MESHLET_LIST_BUILD_INCLUDED