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

struct PBRLighting
{
    static float3 ComputeLightingDirect(const BRDFInput brdfInput)
    {
        return ComputeBRDF(brdfInput);
    }

    static float3 ComputeLightingIndirect(const SurfaceData surfaceData)
    {
        BRDFInput brdfInput;
        brdfInput.normalWS = surfaceData.normalWS;
        brdfInput.lightDirectionWS = 0;
        brdfInput.lightColor = 0;
        brdfInput.positionWS = surfaceData.positionWS;
        brdfInput.cameraPositionWS = GetCameraPositionWS();
        brdfInput.diffuseColor = surfaceData.albedo;
        brdfInput.metallic = surfaceData.metallic;
        brdfInput.roughness = surfaceData.roughness;
        brdfInput.irradiance = SampleDiffuseIrradiance(surfaceData.normalWS);
        brdfInput.ambientOcclusion = 1.0f;

        const float3 indirectDiffuse = ComputeBRDFIndirectDiffuse(brdfInput);
        const float3 indirectSpecular = ComputeBRDFIndirectSpecular(brdfInput);
        return aaaa_AmbientIntensity * (indirectDiffuse + indirectSpecular);
    }
};

#endif // AAAA_GBUFFER_INCLUDED