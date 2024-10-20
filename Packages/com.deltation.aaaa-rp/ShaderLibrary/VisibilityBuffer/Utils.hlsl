#ifndef AAAA_VISIBILITY_BUFFER_UTILS_INCLUDED
#define AAAA_VISIBILITY_BUFFER_UTILS_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"

TYPED_TEXTURE2D(float2, _VisibilityBuffer);

#define VISIBILITY_BUFFER_INDEX_ID_BITS (8u)
#define VISIBILITY_BUFFER_INDEX_ID_MASK ((1u << VISIBILITY_BUFFER_INDEX_ID_BITS) - 1u)

struct VisibilityBufferValue
{
    uint instanceID;
    uint meshletID;
    uint indexID;
};

uint2 PackVisibilityBufferValue(const VisibilityBufferValue value)
{
    return uint2(value.instanceID,
                 value.meshletID << VISIBILITY_BUFFER_INDEX_ID_BITS | (value.indexID / 3) & VISIBILITY_BUFFER_INDEX_ID_MASK);
}

VisibilityBufferValue UnpackVisibilityBufferValue(uint2 packedValue)
{
    VisibilityBufferValue value;
    value.instanceID = packedValue.x;
    value.meshletID = packedValue.y >> VISIBILITY_BUFFER_INDEX_ID_BITS;
    value.indexID = (packedValue.y & VISIBILITY_BUFFER_INDEX_ID_MASK) * 3;
    return value;
}


uint2 SampleVisibilityBuffer(const float2 screenUV)
{
    return asuint(SAMPLE_TEXTURE2D_LOD(_VisibilityBuffer, sampler_PointClamp, screenUV, 0).xy);
}

uint2 LoadVisibilityBuffer(const float2 texCoords)
{
    return asuint(LOAD_TEXTURE2D_LOD(_VisibilityBuffer, texCoords, 0).xy);
}

#endif // AAAA_VISIBILITY_BUFFER_UTILS_INCLUDED