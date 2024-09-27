#ifndef AAAA_LIGHTING_INCLUDED
#define AAAA_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/AmbientProbe.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"

float4 _MainLight_Color;
float4 _MainLight_Direction;

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
    float3 direction;
};

Light GetMainLight()
{
    Light light;
    light.color = _MainLight_Color.rgb;
    light.direction = _MainLight_Direction.xyz;
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