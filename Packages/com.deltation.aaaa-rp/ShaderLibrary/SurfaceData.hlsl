#ifndef AAAA_SURFACE_DATA_INCLUDED
#define AAAA_SURFACE_DATA_INCLUDED

struct SurfaceData
{
    float3 albedo;
    float  metallic;
    float  roughness;
    float3 emission;
    float3 normalWS;
    float  alpha;
};

#endif // AAAA_SURFACE_DATA_INCLUDED