#ifndef AAAA_BRDF_CORE_INCLUDED
#define AAAA_BRDF_CORE_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Math.hlsl"

struct BRDFInput
{
    float3 normalWS;
    float3 positionWS;
    float3 cameraPositionWS;
    float3 diffuseColor;
    float  metallic;
    float  roughness;
    float3 irradiance;
    float3 prefilteredEnvironment;
    float  aoVisibility;
    float3 bentNormalWS;
};

float3 FresnelSchlick(const float cosTheta, const float3 f0)
{
    return f0 + (1.0 - f0) * pow(saturate(1.0 - cosTheta), 5.0);
}

float3 FresnelSchlick(const float cosTheta, const float3 f0, const float roughness)
{
    return f0 + (max(1.0 - roughness, f0) - f0) * pow(saturate(1.0 - cosTheta), 5.0);
}

float DistributionGGX(const float3 n, const float3 h, const float roughness)
{
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH = max(dot(n, h), 0.0);
    float NdotH2 = NdotH * NdotH;

    float num = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;

    return num / denom;
}

float GeometrySchlickGGX(const float NdotV, const float roughness)
{
    float r = (roughness + 1.0);
    float k = (r * r) / 8.0;

    float num = NdotV;
    float denom = NdotV * (1.0 - k) + k;

    return num / denom;
}

float GeometrySmith(const float3 n, const float3 v, const float3 l, const float roughness)
{
    float NdotV = max(dot(n, v), 0.0);
    float NdotL = max(dot(n, l), 0.0);
    float ggx2 = GeometrySchlickGGX(NdotV, roughness);
    float ggx1 = GeometrySchlickGGX(NdotL, roughness);

    return ggx1 * ggx2;
}

float3 ComputeF0(const BRDFInput input)
{
    float3 F0 = 0.04;
    return lerp(F0, input.diffuseColor, input.metallic);
}

float3 ComputeBRDFIndirectDiffuse(const BRDFInput input, const float3 eyeWS)
{
    const float3 F0 = ComputeF0(input);
    const float3 F = FresnelSchlick(max(dot(input.normalWS, eyeWS), 0.0), F0, input.roughness);
    const float3 kS = F;
    float3       kD = 1.0 - kS;
    kD *= 1.0 - input.metallic;

    const float3 diffuse = input.irradiance * input.diffuseColor;
    float3       result = (kD * diffuse);
    result *= input.aoVisibility;

    return result;
}

float3 ComputeBRDFReflectionVector(const float3 normalWS, const float3 eyeWS)
{
    return reflect(-eyeWS, normalWS);
}

#endif // AAAA_BRDF_CORE_INCLUDED