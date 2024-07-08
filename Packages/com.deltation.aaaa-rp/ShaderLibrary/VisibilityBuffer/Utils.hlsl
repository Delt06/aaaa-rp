#ifndef AAAA_VISIBILITY_BUFFER_UTILS_INCLUDED
#define AAAA_VISIBILITY_BUFFER_UTILS_INCLUDED

#define VISIBILITY_BUFFER_INDEX_ID_BITS (8u)
#define VISIBILITY_BUFFER_INDEX_ID_MASK ((1u << VISIBILITY_BUFFER_INDEX_ID_BITS) - 1u) 

uint2 PackVisibilityBufferValue(const uint instanceID, const uint meshletID, const uint indexID)
{
    return uint2(instanceID, meshletID << VISIBILITY_BUFFER_INDEX_ID_BITS | indexID / 3);
}

void UnpackVisibilityBufferValue(uint2 value, out uint instanceID, out uint meshletID, out uint indexID)
{
    instanceID = value.x;
    meshletID = value.y >> VISIBILITY_BUFFER_INDEX_ID_BITS;
    indexID = value.y & VISIBILITY_BUFFER_INDEX_ID_MASK;
}

#endif // AAAA_VISIBILITY_BUFFER_UTILS_INCLUDED