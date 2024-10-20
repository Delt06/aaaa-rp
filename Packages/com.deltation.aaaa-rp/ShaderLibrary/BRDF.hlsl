#ifndef AAAA_BRDF_INCLUDED
#define AAAA_BRDF_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/GTAO.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Lighting.hlsl"
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

// A contact shadow approximation, totally not physically correct; a riff on "Chan 2018, "Material Advances in Call of Duty: WWII" and "The Technical Art of Uncharted 4" http://advances.realtimerendering.com/other/2016/naughty_dog/NaughtyDog_TechArt_Final.pdf (microshadowing)"
float ComputeMicroShadowing(const BRDFInput input, const Light light, float NdotL, const float ao)
{
    #ifdef AAAA_GTAO_BENT_NORMALS
    NdotL = saturate(dot(input.bentNormalWS, light.directionWS));
    #endif

    #if 0 // from the paper  - different from Filament and looks wrong
    float aperture = 2.0 * ao * ao;
    return saturate(abs(NdotL) + aperture - 1.0);
    #else // filament version
    float aperture = rsqrt(1.0000001 - ao);
    NdotL += 0.1; // when using bent normals, avoids overshadowing - bent normals are just approximation anyhow
    return saturate(NdotL * aperture);
    #endif
}

float3 ComputeBRDF(const BRDFInput input, const Light light)
{
    const float3 eyeWS = normalize(input.cameraPositionWS - input.positionWS);
    const float3 halfVectorWS = normalize(eyeWS + light.directionWS);

    const float3 radiance = light.color;

    const float3 F0 = ComputeF0(input);
    const float3 F = FresnelSchlick(max(dot(halfVectorWS, eyeWS), 0.0), F0);

    const float NDF = DistributionGGX(input.normalWS, halfVectorWS, input.roughness);
    const float G = GeometrySmith(input.normalWS, eyeWS, light.directionWS, input.roughness);

    const float3 numerator = NDF * G * F;
    const float  denominator = 4.0 * max(dot(input.normalWS, eyeWS), 0.0) * max(dot(input.normalWS, light.directionWS), 0.0) + 0.0001;
    const float3 specular = numerator / denominator;

    const float3 kS = F;
    float3       kD = 1.0 - kS;

    kD *= 1.0 - input.metallic;

    const float NdotL = saturate(dot(input.normalWS, light.directionWS));
    float       shadowAttenuation = light.shadowAttenuation;

    #ifdef AAAA_DIRECT_LIGHTING_AO_MICROSHADOWS
    float microShadowing = ComputeMicroShadowing(input, light, NdotL, input.aoVisibility);
    microShadowing = lerp(1, microShadowing, light.shadowStrength);
    shadowAttenuation = min(shadowAttenuation, microShadowing);
    #endif

    return shadowAttenuation * (kD * input.diffuseColor / PI + specular) * radiance * NdotL;
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

float3 ComputeBRDFIndirectSpecular(const BRDFInput input, const float3 eyeWS)
{
    const float3 F0 = ComputeF0(input);
    const float  NdotI = max(dot(input.normalWS, eyeWS), 0.0);
    const float3 F = FresnelSchlick(NdotI, F0, input.roughness);

    // https://github.com/JoeyDeVries/LearnOpenGL/blob/master/src/6.pbr/2.2.1.ibl_specular/2.2.1.pbr.fs
    const float2 brdf = SampleBRDFLut(NdotI, input.roughness);
    float3       result = input.prefilteredEnvironment * (F * brdf.x + brdf.y); // A channel stores fade amount
    result *= input.aoVisibility;

    return result;
}

#endif // AAAA_BRDF_INCLUDED