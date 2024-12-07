﻿#ifndef AAAA_LIGHT_PROPAGATION_VOLUMES_INCLUDED
#define AAAA_LIGHT_PROPAGATION_VOLUMES_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Shadows.hlsl"

struct LPVCellValue
{
    float4 redSH;
    float4 greenSH;
    float4 blueSH;
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

    static float4 DirToSH(float3 dir)
    {
        static const float shC0 = 0.282094792f; // 1 / 2sqrt(pi)
        static const float shC1 = 0.488602512f; // sqrt(3/pi) / 2
        return float4(shC0, -shC1 * dir.y, shC1 * dir.z, -shC1 * dir.x);
    }

    static float3 EvaluateRadiance(const LPVCellValue value, const float3 normalWS)
    {
        float4 shIntensity = DirToSH(-normalWS);

        const float3 lpvIntensity = float3(
            dot(shIntensity, value.redSH),
            dot(shIntensity, value.greenSH),
            dot(shIntensity, value.blueSH)
        );

        return max(0, lpvIntensity) / PI;
    }
};

TEXTURE3D(_LPVGridRedSH);
TEXTURE3D(_LPVGridGreenSH);
TEXTURE3D(_LPVGridBlueSH);

int    _LPVGridSize;
float3 _LPVGridBoundsMin;
float3 _LPVGridBoundsMax;

uint GetLPVGridSize()
{
    return _LPVGridSize;
}

float3 ComputeLPVCellCenter(const uint3 cellID)
{
    const float3 positionT = (cellID + 0.5) / _LPVGridSize;
    const float3 cellCenterWS = lerp(_LPVGridBoundsMin, _LPVGridBoundsMax, positionT);
    return cellCenterWS;
}

float3 ComputeLPVGridUV(const float3 positionWS)
{
    return (positionWS - _LPVGridBoundsMin) / (_LPVGridBoundsMax - _LPVGridBoundsMin);
}

int3 ComputeLPVCellID(const float3 positionWS)
{
    float3 uv = ComputeLPVGridUV(positionWS);
    return uv * _LPVGridSize;
}

LPVCellValue SampleLPVGrid(const float3 positionWS)
{
    const float3 uv = ComputeLPVGridUV(positionWS);
    LPVCellValue value;
    value.redSH = SAMPLE_TEXTURE3D_LOD(_LPVGridRedSH, sampler_TrilinearClamp, uv, 0);
    value.greenSH = SAMPLE_TEXTURE3D_LOD(_LPVGridGreenSH, sampler_TrilinearClamp, uv, 0);
    value.blueSH = SAMPLE_TEXTURE3D_LOD(_LPVGridBlueSH, sampler_TrilinearClamp, uv, 0);
    return value;
}

LPVCellValue SampleLPVGrid_PointFilter(const float3 positionWS)
{
    const float3 uv = ComputeLPVGridUV(positionWS);
    LPVCellValue value;
    value.redSH = SAMPLE_TEXTURE3D_LOD(_LPVGridRedSH, sampler_PointClamp, uv, 0);
    value.greenSH = SAMPLE_TEXTURE3D_LOD(_LPVGridGreenSH, sampler_PointClamp, uv, 0);
    value.blueSH = SAMPLE_TEXTURE3D_LOD(_LPVGridBlueSH, sampler_PointClamp, uv, 0);
    return value;
}

#define RSM_SAMPLER sampler_PointClamp

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
    Texture2D positionMap = GetBindlessTexture2D(shadowLightSlice.BindlessRsmPositionMapIndex);
    Texture2D normalMap = GetBindlessTexture2D(shadowLightSlice.BindlessRsmNormalMapIndex);
    Texture2D fluxMap = GetBindlessTexture2D(shadowLightSlice.BindlessRsmFluxMapIndex);

    RsmOutput rsmOutput;
    rsmOutput.positionWS = SAMPLE_TEXTURE2D_LOD(positionMap, RSM_SAMPLER, shadowCoords.xy, 0).rgb;
    rsmOutput.packedNormalWS = SAMPLE_TEXTURE2D_LOD(normalMap, RSM_SAMPLER, shadowCoords.xy, 0).xy;
    rsmOutput.flux = SAMPLE_TEXTURE2D_LOD(fluxMap, RSM_SAMPLER, shadowCoords.xy, 0).rgb;

    return UnpackRsmOutput(rsmOutput);
}

#endif // AAAA_LIGHT_PROPAGATION_VOLUMES_INCLUDED