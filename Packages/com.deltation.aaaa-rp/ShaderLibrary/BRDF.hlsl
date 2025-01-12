#ifndef AAAA_BRDF_INCLUDED
#define AAAA_BRDF_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/BRDFCore.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/GTAO.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Lighting.hlsl"

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