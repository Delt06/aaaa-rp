#ifndef AAAA_VISIBILITY_BUFFER_UTILS_INCLUDED
#define AAAA_VISIBILITY_BUFFER_UTILS_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/MeshLOD.hlsl"

TEXTURE2D(_VisibilityBuffer);
SAMPLER(sampler_VisibilityBuffer);

#define VISIBILITY_BUFFER_INDEX_ID_BITS (8u)
#define VISIBILITY_BUFFER_INDEX_ID_MASK ((1u << VISIBILITY_BUFFER_INDEX_ID_BITS) - 1u)

struct VisibilityBufferValue
{
    uint instanceID;
    uint localNodeIndex;
    uint meshletID;
    uint indexID;
};

uint2 PackVisibilityBufferValue(const VisibilityBufferValue value)
{
    return uint2(PackInstanceID_LocalNodeIndex(value.instanceID, value.localNodeIndex),
                 value.meshletID << VISIBILITY_BUFFER_INDEX_ID_BITS | (value.indexID / 3) & VISIBILITY_BUFFER_INDEX_ID_MASK);
}

VisibilityBufferValue UnpackVisibilityBufferValue(uint2 packedValue)
{
    VisibilityBufferValue value;
    UnpackInstanceID_LocalNodeIndex(packedValue.x, value.instanceID, value.localNodeIndex);
    value.meshletID = packedValue.y >> VISIBILITY_BUFFER_INDEX_ID_BITS;
    value.indexID = (packedValue.y & VISIBILITY_BUFFER_INDEX_ID_MASK) * 3;
    return value;
}


uint2 SampleVisibilityBuffer(const float2 screenUV)
{
    return asuint(SAMPLE_TEXTURE2D_LOD(_VisibilityBuffer, sampler_VisibilityBuffer, screenUV, 0).xy);
}

#endif // AAAA_VISIBILITY_BUFFER_UTILS_INCLUDED