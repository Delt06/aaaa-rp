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
    return diffuse;
}

#endif // AAAA_GBUFFER_INCLUDED