#ifndef AAAA_OCCLUSION_CULLING_INCLUDED
#define AAAA_OCCLUSION_CULLING_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/CameraHZB.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Math.hlsl"

ByteAddressBuffer _OcclusionCulling_PrevInstanceVisibilityMask;

#ifdef RW_OCCLUSION_CULLING_INSTANCE_VISIBILITY_MASK
RWByteAddressBuffer
#else
ByteAddressBuffer
#endif
_OcclusionCulling_InstanceVisibilityMask;

struct OcclusionCulling
{
    static void UnpackInstanceID(const uint instanceID, out uint maskAddress, out uint instanceMask)
    {
        const uint instancesPerItem = 32;
        maskAddress = instanceID / instancesPerItem << 2;
        instanceMask = 1u << instanceID % instancesPerItem;
    }

    static bool WasInstanceVisibleLastFrame(const uint instanceID)
    {
        uint maskAddress;
        uint instanceMask;
        UnpackInstanceID(instanceID, maskAddress, instanceMask);

        const uint item = _OcclusionCulling_PrevInstanceVisibilityMask.Load(maskAddress);
        return (item & instanceMask) != 0;
    }

    static bool WasInstanceVisibleThisFrame(const uint instanceID)
    {
        uint maskAddress;
        uint instanceMask;
        UnpackInstanceID(instanceID, maskAddress, instanceMask);

        const uint item = _OcclusionCulling_InstanceVisibilityMask.Load(maskAddress);
        return (item & instanceMask) != 0;
    }

    #ifdef RW_OCCLUSION_CULLING_INSTANCE_VISIBILITY_MASK
    static void MarkVisibleThisFrame(const uint instanceID)
    {
        uint maskAddress;
        uint instanceMask;
        UnpackInstanceID(instanceID, maskAddress, instanceMask);

        _OcclusionCulling_InstanceVisibilityMask.InterlockedOr(maskAddress, instanceMask);
    }

    static void MarkInvisibleThisFrame(const uint instanceID)
    {
        uint maskAddress;
        uint instanceMask;
        UnpackInstanceID(instanceID, maskAddress, instanceMask);

        _OcclusionCulling_InstanceVisibilityMask.InterlockedAnd(maskAddress, ~instanceMask);
    }
    #endif

    static const uint kAABBVertexCount = 8;

    static BoundingSquareSS ComputeScreenSpaceBoundingSquare(const float3 aabbVertices[kAABBVertexCount], const float4x4 viewProjectionMatrix)
    {
        BoundingSquareSS result;
        result.NDCMinZ = DEPTH_FAR;
        result.MinUV = 1;
        result.MaxUV = 0;

        UNITY_UNROLL
        for (uint i = 0; i < kAABBVertexCount; ++i)
        {
            const float3 ndc = ComputeNormalizedDeviceCoordinatesWithZ(aabbVertices[i], viewProjectionMatrix);

            result.MinUV = min(result.MinUV, ndc.xy);
            result.MaxUV = max(result.MaxUV, ndc.xy);
            result.NDCMinZ = MIN_DEPTH(result.NDCMinZ, ndc.z);
        }

        result.MinUV = saturate(result.MinUV);
        result.MaxUV = saturate(result.MaxUV);

        return result;
    }

    static BoundingSquareSS ComputeScreenSpaceBoundingSquare(const AABB aabb, const float4x4 viewProjectionMatrix)
    {
        const float3 aabbMin = aabb.boundsMin;
        const float3 aabbMax = aabb.boundsMax;

        const float3 aabbVertices[kAABBVertexCount] =
        {
            float3(aabbMin.x, aabbMin.y, aabbMin.z),
            float3(aabbMin.x, aabbMin.y, aabbMax.z),
            float3(aabbMin.x, aabbMax.y, aabbMin.z),
            float3(aabbMin.x, aabbMax.y, aabbMax.z),
            float3(aabbMax.x, aabbMin.y, aabbMin.z),
            float3(aabbMax.x, aabbMin.y, aabbMax.z),
            float3(aabbMax.x, aabbMax.y, aabbMin.z),
            float3(aabbMax.x, aabbMax.y, aabbMax.z),
        };
        return ComputeScreenSpaceBoundingSquare(aabbVertices, viewProjectionMatrix);
    }


    static BoundingSquareSS ComputeScreenSpaceBoundingSquare(const float4 boundingSphere, const float4x4 viewProjectionMatrix)
    {
        const float3 center = boundingSphere.xyz;
        const float  radius = boundingSphere.w;
        const float3 aabbVertices[kAABBVertexCount] =
        {
            center + float3(-radius, -radius, -radius),
            center + float3(-radius, -radius, radius),
            center + float3(-radius, radius, -radius),
            center + float3(-radius, radius, radius),
            center + float3(radius, -radius, -radius),
            center + float3(radius, -radius, radius),
            center + float3(radius, radius, -radius),
            center + float3(radius, radius, radius),
        };
        return ComputeScreenSpaceBoundingSquare(aabbVertices, viewProjectionMatrix);
    }

    static bool IsVisible(const BoundingSquareSS boundingSquareSS)
    {
        const float2 depthTextureSize = _CameraHZBMipRects[0].zw;
        const float2 minCoords = boundingSquareSS.MinUV * depthTextureSize;
        const float2 maxCoords = boundingSquareSS.MaxUV * depthTextureSize;
        const float2 boundsSizePixels = maxCoords - minCoords;
        const int    lod = ceil(log2(max(boundsSizePixels.x, boundsSizePixels.y) * 0.5f));

        const float4 occluderDepths = float4(
            CameraHZB::LoadPadBorders((int2)minCoords, lod),
            CameraHZB::LoadPadBorders((int2)maxCoords, lod),
            CameraHZB::LoadPadBorders(int2(minCoords.x, maxCoords.y), lod),
            CameraHZB::LoadPadBorders(int2(maxCoords.x, minCoords.y), lod)
        );
        const float maxOccluderDepth = MAX_DEPTH(MAX_DEPTH(occluderDepths.x, occluderDepths.y), MAX_DEPTH(occluderDepths.z, occluderDepths.w));
        const bool  isVisible = LEQUAL_DEPTH(boundingSquareSS.NDCMinZ, maxOccluderDepth);

        return isVisible;
    }
};


#endif // AAAA_OCCLUSION_CULLING_INCLUDED