#ifndef AAAA_VXGI_INCLUDED
#define AAAA_VXGI_INCLUDED

// Sources:
// - https://github.com/turanszkij/WickedEngine/blob/4db6c94b2246c298087f10f861c00d9adea13b1d/WickedEngine/shaders/voxelConeTracingHF.hlsli

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Packages/com.deltation.aaaa-rp/Runtime/Lighting/AAAAVxgiConstantBuffer.cs.hlsl"
#include "Packages/com.deltation.aaaa-rp/Runtime/Lighting/AAAAVxgiCommon.cs.hlsl"

#define VXGI_PACKING_PRECISION (1024)

TYPED_TEXTURE3D(float4, _VXGIRadiance);
uint _VXGILevelCount;

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
            Grid        grid;
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

        float3 TransformWorldToGridUV(const float3 positionWS)
        {
            return (positionWS - _VxgiGridBoundsMin.xyz) * (invVoxelSizeWS * invSize);
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

    static const uint  DIFFUSE_CONE_COUNT = 32;
    static const float DIFFUSE_CONE_APERTURE = 0.628319f;

    static const float3 DIFFUSE_CONE_DIRECTIONS[DIFFUSE_CONE_COUNT] = {
        float3(0.898904f, 0.435512f, 0.0479745f),
        float3(0.898904f, -0.435512f, -0.0479745f),
        float3(0.898904f, 0.0479745f, -0.435512f),
        float3(0.898904f, -0.0479745f, 0.435512f),
        float3(-0.898904f, 0.435512f, -0.0479745f),
        float3(-0.898904f, -0.435512f, 0.0479745f),
        float3(-0.898904f, 0.0479745f, 0.435512f),
        float3(-0.898904f, -0.0479745f, -0.435512f),
        float3(0.0479745f, 0.898904f, 0.435512f),
        float3(-0.0479745f, 0.898904f, -0.435512f),
        float3(-0.435512f, 0.898904f, 0.0479745f),
        float3(0.435512f, 0.898904f, -0.0479745f),
        float3(-0.0479745f, -0.898904f, 0.435512f),
        float3(0.0479745f, -0.898904f, -0.435512f),
        float3(0.435512f, -0.898904f, 0.0479745f),
        float3(-0.435512f, -0.898904f, -0.0479745f),
        float3(0.435512f, 0.0479745f, 0.898904f),
        float3(-0.435512f, -0.0479745f, 0.898904f),
        float3(0.0479745f, -0.435512f, 0.898904f),
        float3(-0.0479745f, 0.435512f, 0.898904f),
        float3(0.435512f, -0.0479745f, -0.898904f),
        float3(-0.435512f, 0.0479745f, -0.898904f),
        float3(0.0479745f, 0.435512f, -0.898904f),
        float3(-0.0479745f, -0.435512f, -0.898904f),
        float3(0.57735f, 0.57735f, 0.57735f),
        float3(0.57735f, 0.57735f, -0.57735f),
        float3(0.57735f, -0.57735f, 0.57735f),
        float3(0.57735f, -0.57735f, -0.57735f),
        float3(-0.57735f, 0.57735f, 0.57735f),
        float3(-0.57735f, 0.57735f, -0.57735f),
        float3(-0.57735f, -0.57735f, 0.57735f),
        float3(-0.57735f, -0.57735f, -0.57735f)
    };

    static const float MAX_DISTANCE = 50;

    struct Tracing
    {
        static float4 SampleVoxelGrid(const float3 positionWS, const uint gridLevel, float stepDist, float3 faceOffsets, float3 directionWeights,
                                      uint         precomputedDirection = 0)
        {
            Grid   grid = Grid::LoadLevel(gridLevel);
            float3 gridUV = grid.TransformWorldToGridUV(positionWS);

            // half texel correction is applied to avoid sampling over current grid:
            const float halfTexel = 0.5f * grid.invSize;
            gridUV = clamp(gridUV, halfTexel, 1 - halfTexel);

            float4 sample = SAMPLE_TEXTURE3D_LOD(_VXGIRadiance, sampler_LinearClamp, gridUV, gridLevel);
            sample *= stepDist * grid.invVoxelSizeWS;

            return sample;
        }

        static float4 ConeTrace(const float3 positionWS, const float3 normalWS, const float3 coneDirection, const float coneAperture,
                                const float  stepSize, const uint     precomputedDirection = 0)
        {
            float3 color = 0;
            float  alpha = 0;

            uint gridLevel0 = 0;
            Grid grid0 = Grid::LoadLevel(gridLevel0);

            const float coneCoefficient = 2 * tan(coneAperture * 0.5);

            // We need to offset the cone start position to avoid sampling its own voxel (self-occlusion):
            float  dist = grid0.voxelSizeWS; // offset by cone dir so that first sample of all cones are not the same
            float  stepDist = dist;
            float3 startPos = positionWS + normalWS * grid0.voxelSizeWS;

            float3 anisoDirection = -coneDirection;
            float3 faceOffsets = float3(
                anisoDirection.x > 0 ? 0 : 1,
                anisoDirection.y > 0 ? 2 : 3,
                anisoDirection.z > 0 ? 4 : 5
            ) / (6.0 + DIFFUSE_CONE_COUNT);
            float3 directionWeights = abs(coneDirection);

            // We will break off the loop if the sampling distance is too far for performance reasons:
            while (dist < MAX_DISTANCE && alpha < 1 && gridLevel0 < _VXGILevelCount)
            {
                float3 p0 = startPos + coneDirection * dist;

                float diameter = max(grid0.voxelSizeWS, coneCoefficient * dist);
                float lod = clamp(log2(diameter * grid0.invVoxelSizeWS), gridLevel0, _VXGILevelCount - 1);

                const uint  gridIndex = floor(lod);
                const float gridBlend = frac(lod);

                Grid         grid = Grid::LoadLevel(gridIndex);
                const float3 gridUV = grid.TransformWorldToGridUV(p0);

                if (any(gridUV < 0 || gridUV > 1))
                {
                    gridLevel0++;
                    grid0 = Grid::LoadLevel(gridLevel0);
                    continue;
                }

                float4 sample = SampleVoxelGrid(p0, gridIndex, stepDist, faceOffsets, directionWeights, precomputedDirection);

                if (gridBlend > 0 && gridIndex < _VXGILevelCount - 1)
                {
                    sample = lerp(sample,
                                  SampleVoxelGrid(p0, gridIndex + 1, stepDist, faceOffsets, directionWeights, precomputedDirection),
                                  gridBlend);
                }

                // front-to back blending:
                float a = 1 - alpha;
                color += a * sample.rgb;
                alpha += a * sample.a;

                float stepSizeCurrent = stepSize;
                stepDist = diameter * stepSizeCurrent;

                // step along ray:
                dist += stepDist;
            }

            return float4(color, alpha);
        }

        static float4 ConeTraceDiffuse(const float3 positionWS, const float3 normalWS)
        {
            float4 amount = 0;

            float sum = 0;
            for (uint i = 0; i < DIFFUSE_CONE_COUNT; ++i)
            {
                const float3 coneDirection = DIFFUSE_CONE_DIRECTIONS[i];
                const float  cosTheta = dot(normalWS, coneDirection);
                if (cosTheta <= 0)
                {
                    continue;
                }

                const uint precomputedDirection = 6 + i;
                amount += cosTheta * ConeTrace(positionWS, normalWS, coneDirection, DIFFUSE_CONE_APERTURE, 1, precomputedDirection);
                sum += cosTheta;
            }

            amount /= sum;

            amount.rgb = max(0, amount.rgb);
            amount.a = saturate(amount.a);

            return amount;
        }
    };
}


#endif // AAAA_VXGI_INCLUDED