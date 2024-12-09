#ifndef AAAA_LIGHT_PROPAGATION_VOLUMES_INCLUDED
#define AAAA_LIGHT_PROPAGATION_VOLUMES_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Shadows.hlsl"

#define LPV_CHANNEL_T float4

struct LPVCellValue
{
    LPV_CHANNEL_T redSH;
    LPV_CHANNEL_T greenSH;
    LPV_CHANNEL_T blueSH;
};

/// Source: https://ericpolman.com/2016/06/28/light-propagation-volumes/
struct LPVMath
{
    static float4 DirToCosineLobe(float3 dir)
    {
        static const float shCosLobeC0 = 0.886226925f; // sqrt(pi)/2
        static const float shCosLobeC1 = 1.02332671f; // sqrt(pi/3)
        return float4(shCosLobeC0, -shCosLobeC1 * dir.y, shCosLobeC1 * dir.z, -shCosLobeC1 * dir.x);
    }

    static LPV_CHANNEL_T DirToSH(float3 dir)
    {
        static const float shC0 = 0.282094792f; // 1 / 2sqrt(pi)
        static const float shC1 = 0.488602512f; // sqrt(3/pi) / 2
        return LPV_CHANNEL_T(shC0, -shC1 * dir.y, shC1 * dir.z, -shC1 * dir.x);
    }

    static float3 EvaluateRadiance(const LPVCellValue value, const float3 normalWS)
    {
        LPV_CHANNEL_T shIntensity = DirToSH(-normalWS);
        const float3  lpvIntensity = float3(
            dot(shIntensity, value.redSH),
            dot(shIntensity, value.greenSH),
            dot(shIntensity, value.blueSH)
        );
        return max(0, lpvIntensity) * INV_PI;
    }

    static float EvaluateBlockingPotential(const LPV_CHANNEL_T blockingPotentialSH, const float3 normalWS)
    {
        LPV_CHANNEL_T shIntensity = DirToSH(-normalWS);
        const float   blockingPotential = dot(shIntensity, blockingPotentialSH);
        return saturate(blockingPotential);
    }
};

TYPED_TEXTURE3D(LPV_CHANNEL_T, _LPVGridRedSH);
TYPED_TEXTURE3D(LPV_CHANNEL_T, _LPVGridGreenSH);
TYPED_TEXTURE3D(LPV_CHANNEL_T, _LPVGridBlueSH);

int    _LPVGridSize;
float3 _LPVGridBoundsMin;
float3 _LPVGridBoundsMax;

struct LPV
{
    static uint GetGridSize()
    {
        return _LPVGridSize;
    }

    static float3 ComputeCellCenter(const uint3 cellID)
    {
        const float3 positionT = (cellID + 0.5) / _LPVGridSize;
        const float3 cellCenterWS = lerp(_LPVGridBoundsMin, _LPVGridBoundsMax, positionT);
        return cellCenterWS;
    }

    static float3 ComputeGridUV(const float3 positionWS)
    {
        return (positionWS - _LPVGridBoundsMin) / (_LPVGridBoundsMax - _LPVGridBoundsMin);
    }

    static int3 ComputeCellID(const float3 positionWS)
    {
        float3 uv = ComputeGridUV(positionWS);
        return uv * _LPVGridSize;
    }

    static uint3 FlatCellIDTo3D(uint flatCellID)
    {
        const uint gridSize = _LPVGridSize;
        uint3      result;
        result.x = flatCellID % gridSize;
        result.y = flatCellID / gridSize % gridSize;
        result.z = flatCellID / (gridSize * gridSize);
        return result;
    }

    static LPVCellValue SampleGrid(const float3 positionWS, const SamplerState samplerState)
    {
        const float3 uv = ComputeGridUV(positionWS);
        LPVCellValue value;
        value.redSH = SAMPLE_TEXTURE3D_LOD(_LPVGridRedSH, samplerState, uv, 0);
        value.greenSH = SAMPLE_TEXTURE3D_LOD(_LPVGridGreenSH, samplerState, uv, 0);
        value.blueSH = SAMPLE_TEXTURE3D_LOD(_LPVGridBlueSH, samplerState, uv, 0);
        return value;
    }

    static LPVCellValue SampleGrid(const float3 positionWS)
    {
        return SampleGrid(positionWS, sampler_TrilinearClamp);
    }
};

struct RsmOutput
{
    float3 positionWS : SV_Target0;
    float2 packedNormalWS : SV_Target1;
    float3 flux : SV_Target2;
};

struct RsmValue
{
    float3 positionWS;
    float3 normalWS;
    float3 flux;
};

float2 PackRsmNormal(const float3 normal)
{
    return PackNormalOctQuadEncode(normal);
}

float3 UnpackRsmNormal(const float2 packedNormal)
{
    return UnpackNormalOctQuadEncode(packedNormal);
}

RsmOutput PackRsmOutput(const RsmValue value)
{
    RsmOutput output;
    output.positionWS = value.positionWS;
    output.packedNormalWS = PackRsmNormal(value.normalWS);
    output.flux = value.flux;
    return output;
}

RsmValue UnpackRsmOutput(const RsmOutput output)
{
    RsmValue value;
    value.positionWS = output.positionWS;
    value.normalWS = UnpackRsmNormal(output.packedNormalWS);
    value.flux = output.flux;
    return value;
}

RsmValue SampleRsmValue(const AAAAShadowLightSlice shadowLightSlice, const float2 shadowCoords)
{
    const Texture2D    positionMap = GetBindlessTexture2D(shadowLightSlice.BindlessRsmPositionMapIndex);
    const Texture2D    normalMap = GetBindlessTexture2D(shadowLightSlice.BindlessRsmNormalMapIndex);
    const Texture2D    fluxMap = GetBindlessTexture2D(shadowLightSlice.BindlessRsmFluxMapIndex);
    const SamplerState rsmSampler = sampler_PointClamp;

    RsmOutput rsmOutput;
    rsmOutput.positionWS = SAMPLE_TEXTURE2D_LOD(positionMap, rsmSampler, shadowCoords.xy, 0).rgb;
    rsmOutput.packedNormalWS = SAMPLE_TEXTURE2D_LOD(normalMap, rsmSampler, shadowCoords.xy, 0).xy;
    rsmOutput.flux = SAMPLE_TEXTURE2D_LOD(fluxMap, rsmSampler, shadowCoords.xy, 0).rgb;

    return UnpackRsmOutput(rsmOutput);
}

#endif // AAAA_LIGHT_PROPAGATION_VOLUMES_INCLUDED