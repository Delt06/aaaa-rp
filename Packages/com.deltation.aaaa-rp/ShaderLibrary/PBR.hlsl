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
    float  aoVisibility;
    float3 bentNormalWS;
};

#endif // AAAA_GBUFFER_INCLUDED