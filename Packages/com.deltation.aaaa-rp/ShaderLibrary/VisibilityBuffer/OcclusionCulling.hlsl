#ifndef AAAA_OCCLUSION_CULLING_INCLUDED
#define AAAA_OCCLUSION_CULLING_INCLUDED

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
    #endif
};

#endif // AAAA_OCCLUSION_CULLING_INCLUDED