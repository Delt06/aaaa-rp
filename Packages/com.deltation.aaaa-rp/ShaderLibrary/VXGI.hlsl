#ifndef AAAA_VXGI_INCLUDED
#define AAAA_VXGI_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Packages/com.deltation.aaaa-rp/Runtime/Lighting/AAAAVxgiConstantBuffer.cs.hlsl"
#include "Packages/com.deltation.aaaa-rp/Runtime/Lighting/AAAAVxgiCommon.cs.hlsl"

#define VXGI_PACKING_PRECISION (1024)

namespace VXGI
{
    struct Packing
    {
        static float2 PackNormal(const float3 normal)
        {
            return PackNormalOctQuadEncode(normal) * 0.5 + 0.5;
        }

        static float3 UnpackNormal(const float2 packedNormal)
        {
            return UnpackNormalOctQuadEncode(packedNormal) * 2.0 - 1.0;
        }

        static uint PackChannel(const float value)
        {
            return (uint)(value * VXGI_PACKING_PRECISION);
        }

        static float UnpackChannel(const uint packedValue)
        {
            return float(packedValue) / VXGI_PACKING_PRECISION;
        }
    };

    struct Grid
    {
        float size;
        float invSize;
        float voxelSizeWS;
        float invVoxelSizeWS;

        static Grid LoadLevel(uint mipLevel)
        {
            Grid grid;
            const float sizeFactor = 1 << mipLevel;
            const float invSizeFactor = 1.0f / sizeFactor;
            grid.size = _VxgiGridResolution.x * invSizeFactor;
            grid.invSize = _VxgiGridResolution.y * sizeFactor;
            grid.voxelSizeWS = _VxgiGridResolution.z * sizeFactor;
            grid.invVoxelSizeWS = _VxgiGridResolution.w * invSizeFactor;
            return grid;
        }

        float3 TransformWorldToGridSpace(const float3 positionWS)
        {
            return (positionWS - _VxgiGridBoundsMin.xyz) * invVoxelSizeWS;
        }

        float3 TransformGridToWorldSpace(const float3 voxelID)
        {
            return voxelID * voxelSizeWS + _VxgiGridBoundsMin.xyz;
        }

        int VoxelToFlatID(const uint3 voxelID)
        {
            const uint uSize = size;
            return voxelID.z * uSize * uSize + voxelID.y * uSize + voxelID.x;
        }

        uint3 FlatToVoxelID(const uint flatID)
        {
            const uint uSize = size;
            return uint3(
                flatID % uSize,
                flatID / uSize % uSize,
                flatID / (uSize * uSize)
            );
        }

        static uint FlatIDToPackedGridAddress(const uint flatID)
        {
            return (flatID * AAAAVXGIPACKEDGRIDCHANNELS_TOTAL_COUNT) << 2;
        }
    };
}


#endif // AAAA_VXGI_INCLUDED