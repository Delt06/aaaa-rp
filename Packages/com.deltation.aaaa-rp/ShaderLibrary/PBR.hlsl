#ifndef AAAA_PBR_INCLUDED
#define AAAA_PBR_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Lighting.hlsl"

struct SurfaceData
{
    float3 albedo;
    float3 normalWS;
};

float3 ComputeLightingPBR(const SurfaceData surfaceData)
{
    const Light  mainLight = GetMainLight();
    const float3 diffuse = surfaceData.albedo * mainLight.color * saturate(dot(surfaceData.normalWS, mainLight.direction));
    const float3 ambient = surfaceData.albedo * SampleSH_AAAA(surfaceData.normalWS);
    return diffuse + ambient;
}

#endif // AAAA_GBUFFER_INCLUDED