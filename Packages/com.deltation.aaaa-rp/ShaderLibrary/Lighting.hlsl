#ifndef AAAA_LIGHTING_INCLUDED
#define AAAA_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/AmbientProbe.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.deltation.aaaa-rp/Runtime/Lighting/AAAALightingConstantBuffer.cs.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/ClusteredLighting.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/PunctualLights.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Shadows.hlsl"

#include "Packages/com.unity.render-pipelines.core/Runtime/Lighting/ProbeVolume/ProbeVolume.hlsl"

TEXTURECUBE(aaaa_DiffuseIrradianceCubemap);
SAMPLER(sampleraaaa_DiffuseIrradianceCubemap);

float aaaa_AmbientIntensity;

TEXTURE2D(aaaa_BRDFLut);

TEXTURECUBE(aaaa_PreFilteredEnvironmentMap);
float aaaa_PreFilteredEnvironmentMap_MaxLOD;

struct Light
{
    float3 color;
    float3 directionWS;
    float  distanceAttenuation;
    float  shadowAttenuation;
    float  shadowStrength;
};

struct ShadowParams
{
    bool  isSoftShadow;
    float shadowStrength;

    static ShadowParams Unpack(const float4 packedParams)
    {
        ShadowParams shadowParams;
        shadowParams.isSoftShadow = packedParams.x;
        shadowParams.shadowStrength = packedParams.y;
        return shadowParams;
    }
};

Light GetDirectionalLight(const uint index, const float3 positionWS)
{
    Light light;
    light.color = DirectionalLightColors[index].rgb;
    light.directionWS = DirectionalLightDirections[index].xyz;
    light.distanceAttenuation = 1.0;

    const ShadowParams                         shadowParams = ShadowParams::Unpack(DirectionalLightShadowParams[index]);
    const float4                               shadowSliceRange_shadowFadeParams = DirectionalLightShadowSliceRanges_ShadowFadeParams[index];
    const float2                               sliceRange = shadowSliceRange_shadowFadeParams.xy;
    const float2                               fadeParams = shadowSliceRange_shadowFadeParams.zw;
    const CascadedDirectionalLightShadowSample shadowSample = SampleCascadedDirectionalLightShadow(
        positionWS, sliceRange, fadeParams, shadowParams.isSoftShadow, shadowParams.shadowStrength);
    light.shadowAttenuation = shadowSample.shadowAttenuation;
    light.shadowStrength = shadowParams.shadowStrength;

    return light;
}

uint GetDirectionalLightCount()
{
    return DirectionalLightCount;
}

Light GetPunctualLight(const uint index, const float3 positionWS)
{
    const AAAAPunctualLightData punctualLightData = _PunctualLights[index];

    const float3 offset = punctualLightData.PositionWS.xyz - positionWS;
    const float  distanceSqr = max(dot(offset, offset), HALF_MIN);
    const float3 lightDirection = offset * rsqrt(distanceSqr);
    const float  distanceAttenuation = DistanceAttenuation(distanceSqr, punctualLightData.Attenuations.x);
    const float  angleAttenuation = AngleAttenuation(punctualLightData.SpotDirection_Angle.xyz, lightDirection, punctualLightData.Attenuations.yz);

    Light light;
    light.color = punctualLightData.Color_Radius.xyz;
    light.directionWS = lightDirection;
    light.distanceAttenuation = distanceAttenuation * angleAttenuation;
    light.shadowAttenuation = 1;
    light.shadowStrength = 1;

    const float sliceIndex = punctualLightData.ShadowSliceIndex_ShadowFadeParams.x;

    UNITY_BRANCH
    if (sliceIndex != -1)
    {
        const bool         isSpot = punctualLightData.SpotDirection_Angle.w > 0.0f;
        const ShadowParams shadowParams = ShadowParams::Unpack(punctualLightData.ShadowParams);
        const float2       fadeParams = punctualLightData.ShadowSliceIndex_ShadowFadeParams.zw;

        ShadowSample shadowSample;

        UNITY_BRANCH
        if (isSpot)
        {
            shadowSample = SampleSpotLightShadow(positionWS, sliceIndex, fadeParams, shadowParams.isSoftShadow,
                                                 shadowParams.shadowStrength);
        }
        else
        {
            shadowSample = SamplePointLightShadow(positionWS, lightDirection, sliceIndex, fadeParams, shadowParams.isSoftShadow,
                                                  shadowParams.shadowStrength);
        }

        light.shadowAttenuation = shadowSample.shadowAttenuation;
        light.shadowStrength = shadowParams.shadowStrength;
    }

    return light;
}

float3 SampleDiffuseIrradiance(const float3 normalWS)
{
    return SAMPLE_TEXTURECUBE(aaaa_DiffuseIrradianceCubemap, sampleraaaa_DiffuseIrradianceCubemap, normalWS).rgb;
}

float2 SampleBRDFLut(const float NdotI, const float roughness)
{
    return SAMPLE_TEXTURE2D(aaaa_BRDFLut, sampler_LinearClamp, float2(NdotI, roughness)).rg;
}

float3 SamplePrefilteredEnvironment(const float3 reflectionWS, const float roughness)
{
    return SAMPLE_TEXTURECUBE_LOD(aaaa_PreFilteredEnvironmentMap, sampler_TrilinearClamp, reflectionWS,
                                  roughness * aaaa_PreFilteredEnvironmentMap_MaxLOD).rgb;
}

float3 SampleProbeVolumePixel(const float3 absolutePositionWS, const float3 normalWS, const float3 viewDir, const float2 positionSS, const uint renderingLayer)
{
    #if defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2)
    float3 bakedGI;
    if (_EnableProbeVolumes)
    {
        EvaluateAdaptiveProbeVolume(absolutePositionWS, normalWS, viewDir, positionSS, renderingLayer, bakedGI);
    }
    else
    {
        bakedGI = EvaluateAmbientProbe(normalWS);
    }
    #ifdef UNITY_COLORSPACE_GAMMA
    bakedGI = LinearToSRGB(bakedGI);
    #endif
    return bakedGI;
    #else
    return float3(0, 0, 0);
    #endif
}

float3 SampleDiffuseGI(const float3 absolutePositionWS, const float3 normalWS, const float3 viewDir, const float2 positionSS, const uint renderingLayer)
{
    #if defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2)
    return SampleProbeVolumePixel(absolutePositionWS, normalWS, viewDir, positionSS, renderingLayer);
    #else
    return SampleDiffuseIrradiance(normalWS);
    #endif
}

#endif // AAAA_GBUFFER_INCLUDED