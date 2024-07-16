#ifndef AAAA_MATH_INCLUDED
#define AAAA_MATH_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"

#define DEFINE_AGGREGATE_FUNC_8(name, type, func) \
    type name(const type x1, const type x2, const type x3, const type x4, const type x5, const type x6, const type x7, const type x8) \
    {\
        const type x12 = func(x1, x2);\
        const type x34 = func(x3, x4);\
        const type x56 = func(x5, x6);\
        const type x78 = func(x7, x8);\
        const type x1234 = func(x12, x34);\
        const type x5678 = func(x56, x78);\
        return func(x1234, x5678);\
    }

DEFINE_AGGREGATE_FUNC_8(Min8, float, min)
DEFINE_AGGREGATE_FUNC_8(Max8, float, max)
DEFINE_AGGREGATE_FUNC_8(Min8, float2, min)
DEFINE_AGGREGATE_FUNC_8(Max8, float2, max)

struct AABB
{
    float3 boundsMin;
    float3 boundsMax;

    static AABB Create(const float3 boundsMin, const float3 boundsMax)
    {
        AABB aabb;
        aabb.boundsMin = boundsMin;
        aabb.boundsMax = boundsMax;
        return aabb;
    }
};

// Based on:
// - https://zeux.io/2023/01/12/approximate-projected-bounds/
float2 AABBScreenSize(const AABB aabbWS, const float4x4 viewProjection)
{
    const float4 sizeX = mul(viewProjection, float4(aabbWS.boundsMax.x - aabbWS.boundsMin.x, 0.0, 0.0, 0.0));
    const float4 sizeY = mul(viewProjection, float4(0.0, aabbWS.boundsMax.y - aabbWS.boundsMin.y, 0.0, 0.0));
    const float4 sizeZ = mul(viewProjection, float4(0.0, 0.0, aabbWS.boundsMax.z - aabbWS.boundsMin.z, 0.0));

    const float4 p0 = mul(viewProjection, float4(aabbWS.boundsMin.x, aabbWS.boundsMin.y, aabbWS.boundsMin.z, 1.0));
    const float4 p1 = p0 + sizeZ;
    const float4 p2 = p0 + sizeY;
    const float4 p3 = p2 + sizeZ;
    const float4 p4 = p0 + sizeX;
    const float4 p5 = p4 + sizeZ;
    const float4 p6 = p4 + sizeY;
    const float4 p7 = p6 + sizeZ;

    float4 boundsSS;
    boundsSS.xy = Min8(
        p0.xy / p0.w, p1.xy / p1.w, p2.xy / p2.w, p3.xy / p3.w,
        p4.xy / p4.w, p5.xy / p5.w, p6.xy / p6.w, p7.xy / p7.w);
    boundsSS.zw = Max8(
        p0.xy / p0.w, p1.xy / p1.w, p2.xy / p2.w, p3.xy / p3.w,
        p4.xy / p4.w, p5.xy / p5.w, p6.xy / p6.w, p7.xy / p7.w);

    // clip space -> uv space
    boundsSS = boundsSS.xwzy * float4(0.5f, -0.5f, 0.5f, -0.5f) + 0.5f;

    return boundsSS.zw - boundsSS.xy;
}

float4 TransformBoundingSphere(const float4 boundingSphereOS, const float4x4 objectToWorldMatrix)
{
    const float3 centerWS = mul(objectToWorldMatrix, float4(boundingSphereOS.xyz, 1)).xyz;
    const float3 offsetWS = mul(objectToWorldMatrix, float4(boundingSphereOS.w, 0, 0, 0)).xyz;
    const float  radiusWS = length(offsetWS);
    return float4(centerWS, radiusWS);
}

AABB TransformAABB(const AABB aabbOS, const float4x4 objectToWorldMatrix)
{
    static const uint aabbCornerCount = 8;
    float3            aabbCorners[aabbCornerCount];
    aabbCorners[0] = float3(aabbOS.boundsMin.x, aabbOS.boundsMin.y, aabbOS.boundsMin.z);
    aabbCorners[1] = float3(aabbOS.boundsMin.x, aabbOS.boundsMin.y, aabbOS.boundsMax.z);
    aabbCorners[2] = float3(aabbOS.boundsMin.x, aabbOS.boundsMax.y, aabbOS.boundsMin.z);
    aabbCorners[3] = float3(aabbOS.boundsMin.x, aabbOS.boundsMax.y, aabbOS.boundsMax.z);
    aabbCorners[4] = float3(aabbOS.boundsMax.x, aabbOS.boundsMin.y, aabbOS.boundsMin.z);
    aabbCorners[5] = float3(aabbOS.boundsMax.x, aabbOS.boundsMin.y, aabbOS.boundsMax.z);
    aabbCorners[6] = float3(aabbOS.boundsMax.x, aabbOS.boundsMax.y, aabbOS.boundsMin.z);
    aabbCorners[7] = float3(aabbOS.boundsMax.x, aabbOS.boundsMax.y, aabbOS.boundsMax.z);

    float3 aabbMinWS = float3(FLT_MAX, FLT_MAX, FLT_MAX);
    float3 aabbMaxWS = float3(-FLT_MAX, -FLT_MAX, -FLT_MAX);

    UNITY_UNROLL
    for (uint i = 0; i < aabbCornerCount; ++i)
    {
        const float3 aabbCornerWS = mul(objectToWorldMatrix, float4(aabbCorners[i], 1.0f)).xyz;

        aabbMinWS = min(aabbMinWS, aabbCornerWS);
        aabbMaxWS = max(aabbMaxWS, aabbCornerWS);
    }

    return AABB::Create(aabbMinWS, aabbMaxWS);
}

float4 AABBToBoundingSphere(const AABB aabb)
{
    const float3 center = (aabb.boundsMax + aabb.boundsMin) * 0.5f;
    const float3 extents = (aabb.boundsMax - aabb.boundsMin) * 0.5f;
    return float4(center, length(extents));
}

#endif // AAAA_MATH_INCLUDED