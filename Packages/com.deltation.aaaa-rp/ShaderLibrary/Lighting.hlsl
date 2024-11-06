#ifndef AAAA_LIGHTING_INCLUDED
#define AAAA_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/AmbientProbe.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.deltation.aaaa-rp/Runtime/Lighting/AAAALightingConstantBuffer.cs.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/ClusteredLighting.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/PunctualLights.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Shadows.hlsl"

float4 aaaa_SHAr;
float4 aaaa_SHAg;
float4 aaaa_SHAb;
float4 aaaa_SHBr;
float4 aaaa_SHBg;
float4 aaaa_SHBb;
float4 aaaa_SHC;

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

    const bool isSpot = punctualLightData.SpotDirection_Angle.w > 0.0f;
    UNITY_BRANCH
    if (isSpot)
    {
        const ShadowParams shadowParams = ShadowParams::Unpack(punctualLightData.ShadowParams);
        const float        sliceIndex = punctualLightData.ShadowSliceIndex_ShadowFadeParams.x;
        if (sliceIndex != -1)
        {
            const float2       fadeParams = punctualLightData.ShadowSliceIndex_ShadowFadeParams.zw;
            const ShadowSample shadowSample = SampleSpotLightShadow(positionWS, sliceIndex, fadeParams, shadowParams.isSoftShadow,
                                                                    shadowParams.shadowStrength);
            light.shadowAttenuation = shadowSample.shadowAttenuation;
            light.shadowStrength = shadowParams.shadowStrength;
        }
        else
        {
            light.shadowAttenuation = 1;
            light.shadowStrength = 1;
        }
    }
    else
    {
        light.shadowAttenuation = 1;
        light.shadowStrength = 1;
    }

    return light;
}

float3 SampleSH_AAAA(const float3 normalWS)
{
    real4 shCoefficients[7];
    shCoefficients[0] = aaaa_SHAr;
    shCoefficients[1] = aaaa_SHAg;
    shCoefficients[2] = aaaa_SHAb;
    shCoefficients[3] = aaaa_SHBr;
    shCoefficients[4] = aaaa_SHBg;
    shCoefficients[5] = aaaa_SHBb;
    shCoefficients[6] = aaaa_SHC;

    return max(float3(0, 0, 0), SampleSH9(shCoefficients, normalWS));
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

#endif // AAAA_GBUFFER_INCLUDED