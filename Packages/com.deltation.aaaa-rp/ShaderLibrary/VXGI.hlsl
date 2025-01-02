#ifndef AAAA_VXGI_INCLUDED
#define AAAA_VXGI_INCLUDED

// Sources:
// - https://github.com/turanszkij/WickedEngine/blob/4db6c94b2246c298087f10f861c00d9adea13b1d/WickedEngine/shaders/voxelConeTracingHF.hlsli
// - https://github.com/godotengine/godot/blob/2582793d408ade0b6ed42f913ae33e7da5fb9184/servers/rendering/renderer_rd/shaders/environment/voxel_gi.glsl

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/BRDFCore.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Packages/com.deltation.aaaa-rp/Runtime/Lighting/AAAAVxgiConstantBuffer.cs.hlsl"
#include "Packages/com.deltation.aaaa-rp/Runtime/Lighting/AAAAVxgiCommon.cs.hlsl"

#define VXGI_PACKING_PRECISION (1024)
#define VXGI_RADIANCE_RANGE (32.0)
#define VXGI_RADIANCE_RANGE_INV (1.0 / VXGI_RADIANCE_RANGE)

TYPED_TEXTURE3D(float4, _VXGIRadiance);
TYPED_TEXTURE2D(float4, _VXGIIndirectDiffuseTexture);
TYPED_TEXTURE2D(float4, _VXGIIndirectSpecularTexture);

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

        static float4 PackRadiance(const float4 radiance)
        {
            return float4(radiance.rgb * VXGI_RADIANCE_RANGE_INV, radiance.a);
        }

        static float4 UnpackRadiance(const float4 packedRadiance)
        {
            return float4(packedRadiance.rgb * VXGI_RADIANCE_RANGE, packedRadiance.a);
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

    static const float TRACE_MAX_DISTANCE = 50;
    static const float TRACE_ALPHA_THRESHOLD = 1;

    // Source: https://github.com/godotengine/godot/blob/2582793d408ade0b6ed42f913ae33e7da5fb9184/servers/rendering/renderer_rd/shaders/environment/voxel_gi.glsl#L397
    static const uint  DIFFUSE_CONE_COUNT = 6;
    static const float DIFFUSE_CONE_TAN_HALF_ANGLE = 0.577;

    static const float3 DIFFUSE_CONE_DIRECTIONS[DIFFUSE_CONE_COUNT] =
    {
        float3(0.0, 0.0, 1.0),
        float3(0.866025, 0.0, 0.5),
        float3(0.267617, 0.823639, 0.5),
        float3(-0.700629, 0.509037, 0.5),
        float3(-0.700629, -0.509037, 0.5),
        float3(0.267617, -0.823639, 0.5),
    };

    struct Tracing
    {
        static float4 SampleVoxelGrid(const float3 positionWS, const float gridLevel, float stepDist)
        {
            Grid   grid = Grid::LoadLevel(gridLevel);
            float3 gridUV = grid.TransformWorldToGridUV(positionWS);

            // half texel correction is applied to avoid sampling over current grid:
            const float halfTexel = 0.5f * grid.invSize;
            gridUV = clamp(gridUV, halfTexel, 1 - halfTexel);

            float4 sample = Packing::UnpackRadiance(SAMPLE_TEXTURE3D_LOD(_VXGIRadiance, sampler_TrilinearClamp, gridUV, gridLevel));
            sample *= stepDist * grid.invVoxelSizeWS;

            return sample;
        }

        static float4 ConeTrace(const float3 positionWS, const float3 normalWS, const float3 coneDirection, const float apertureTanHalfAngle,
                                const float  stepSize)
        {
            float3 color = 0;
            float  alpha = 0;

            uint gridLevel0 = 0;
            Grid grid0 = Grid::LoadLevel(gridLevel0);

            // We need to offset the cone start position to avoid sampling its own voxel (self-occlusion):
            float  dist = grid0.voxelSizeWS; // offset by cone dir so that first sample of all cones are not the same
            float  stepDist = dist;
            float3 startPos = positionWS + normalWS * grid0.voxelSizeWS;

            // We will break off the loop if the sampling distance is too far for performance reasons:
            while (dist < TRACE_MAX_DISTANCE && alpha < TRACE_ALPHA_THRESHOLD && gridLevel0 < _VxgiLevelCount)
            {
                grid0 = Grid::LoadLevel(gridLevel0);

                const float diameter = max(grid0.voxelSizeWS, 2.0 * apertureTanHalfAngle * dist);
                const float gridLevel = clamp(log2(diameter * grid0.invVoxelSizeWS), gridLevel0, _VxgiLevelCount - 1);

                Grid         grid = Grid::LoadLevel(gridLevel);
                const float3 p0 = startPos + coneDirection * dist;
                const float3 gridUV = grid.TransformWorldToGridUV(p0);

                if (any(gridUV < 0 || gridUV > 1))
                {
                    gridLevel0++;
                    continue;
                }

                const float4 sample = SampleVoxelGrid(p0, gridLevel, stepDist);

                // front-to back blending:
                float a = 1 - alpha;
                color += a * sample.rgb;
                alpha += a * sample.a;

                stepDist = diameter * stepSize;

                // step along ray:
                dist += stepDist;
            }

            return float4(color, alpha);
        }

        static float3x3 CreateTangentBasis(const float3 normal)
        {
            // Choose an arbitrary vector to start the tangent calculation.
            const float3 arbitrary = abs(normal.z) < 0.999f ? float3(0.0f, 0.0f, 1.0f) : float3(1.0f, 0.0f, 0.0f);

            // Calculate the tangent and bitangent.
            const float3 tangent = normalize(cross(arbitrary, normal));
            const float3 bitangent = cross(normal, tangent);

            return float3x3(tangent, bitangent, normal);
        }

        static float4 ConeTraceDiffuse(const float3 positionWS, const float3 normalWS)
        {
            const float3x3 tangentToWorld = CreateTangentBasis(normalWS);

            float4 amount = 0;
            float  sum = 0;

            UNITY_UNROLL
            for (uint i = 0; i < DIFFUSE_CONE_COUNT; ++i)
            {
                const float3 coneDirectionTS = DIFFUSE_CONE_DIRECTIONS[i];
                const float3 coneDirectionWS = normalize(TransformTangentToWorld(coneDirectionTS, tangentToWorld));
                const float  cosTheta = dot(normalWS, coneDirectionWS);
                amount += cosTheta * ConeTrace(positionWS, normalWS, coneDirectionWS, DIFFUSE_CONE_TAN_HALF_ANGLE, 1);
                sum += cosTheta;
            }

            amount /= sum;
            amount.a = saturate(amount.a * _VxgiDiffuseOpacityFactor);

            return amount;
        }

        static float4 ConeTraceSpecular(const float3 positionWS, const float3 normalWS, const float roughness)
        {
            const float3 cameraPositionWS = GetCameraPositionWS();
            const float3 eyeWS = normalize(cameraPositionWS - positionWS);
            const float3 reflectionWS = ComputeBRDFReflectionVector(normalWS, eyeWS);
            const float  apertureTanHalfAngle = tan(roughness * 0.5);

            float4 amount = ConeTrace(positionWS + reflectionWS * 0.5, normalWS, reflectionWS, apertureTanHalfAngle, 1);
            amount.a = saturate(amount.a * _VxgiSpecularOpacityFactor);

            return amount;
        }

        static float4 LoadIndirectDiffuse(const float2 positionSS)
        {
            return SAMPLE_TEXTURE2D_LOD(_VXGIIndirectDiffuseTexture, sampler_LinearClamp, positionSS * _ScreenSize.zw, 0);
        }

        static float4 LoadIndirectSpecular(const float2 screenUV)
        {
            return SAMPLE_TEXTURE2D_LOD(_VXGIIndirectSpecularTexture, sampler_LinearClamp, screenUV, 0);
        }
    };
}


#endif // AAAA_VXGI_INCLUDED