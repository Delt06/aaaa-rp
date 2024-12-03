#ifndef AAAA_LIGHT_PROPAGATION_VOLUMES_INCLUDED
#define AAAA_LIGHT_PROPAGATION_VOLUMES_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Shadows.hlsl"

TEXTURE2D(_LPVTraceResult);

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

float3 SampleLightPropagationVolumes(const float2 screenUV)
{
    return SAMPLE_TEXTURE2D_LOD(_LPVTraceResult, sampler_LinearClamp, screenUV, 0).rgb;
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