#ifndef AAAA_PBR_INCLUDED
#define AAAA_PBR_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/BRDF.hlsl"

struct SurfaceData
{
    float3 albedo;
    float  roughness;
    float  metallic;
    float3 normalWS;
    float3 positionWS;
};

float3 ComputeLightingPBR(const SurfaceData surfaceData)
{
    const Light mainLight = GetMainLight();

    BRDFInput brdfInput;
    brdfInput.normalWS = surfaceData.normalWS;
    brdfInput.lightDirectionWS = mainLight.direction;
    brdfInput.lightColor = mainLight.color;
    brdfInput.positionWS = surfaceData.positionWS;
    brdfInput.cameraPositionWS = GetCameraPositionWS();
    brdfInput.diffuseColor = surfaceData.albedo;
    brdfInput.metallic = surfaceData.metallic;
    brdfInput.roughness = surfaceData.roughness;
    brdfInput.irradiance = SampleDiffuseIrradiance(surfaceData.normalWS);
    brdfInput.ambientOcclusion = 1.0f;

    const float3 direct = ComputeBRDF(brdfInput);
    const float3 indirectDiffuse = ComputeBRDFIndirectDiffuse(brdfInput);
    const float3 indirectSpecular = ComputeBRDFIndirectSpecular(brdfInput);
    const float3 lighting = direct + aaaa_AmbientIntensity * (indirectDiffuse + indirectSpecular);
    return lighting;
}

#endif // AAAA_GBUFFER_INCLUDED