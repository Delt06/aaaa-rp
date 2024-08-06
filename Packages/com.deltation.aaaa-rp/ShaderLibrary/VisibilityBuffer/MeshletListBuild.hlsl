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

WorkNode LoadWorkNode(ByteAddressBuffer buffer, const uint index)
{
    uint3 value = buffer.Load3(index * WORK_NODE_STRIDE);
    WorkNode node;
    node.InstanceID = value.x;
    node.MeshLODNodeIndex = value.y;
    node.VisitedMaskOffset = value.z;
    return node;
}

void StoreWorkNode(RWByteAddressBuffer buffer, const uint index, const WorkNode node)
{
    buffer.Store3(index * WORK_NODE_STRIDE, uint3(node.InstanceID, node.MeshLODNodeIndex, node.VisitedMaskOffset));
}

#endif // AAAA_MESHLET_LIST_BUILD_INCLUDED
