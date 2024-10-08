#ifndef AAAA_GTAO_INCLUDED
#define AAAA_GTAO_INCLUDED

#ifdef AAAA_GTAO
#define MULTI_BOUNCE_AMBIENT_OCCLUSION 1
#endif

Texture2D<uint> _GTAOTerm;

float SampleGTAO(const uint2 pixelCoords)
{
    #ifdef AAAA_GTAO
    return (LOAD_TEXTURE2D(_GTAOTerm, pixelCoords).r / 255.0);
    #else
    return 1;
    #endif
}

float SpecularAO_Lagarde(float NoV, float visibility, float roughness)
{
    // Lagarde and de Rousiers 2014, "Moving Frostbite to PBR"
    return saturate(pow(NoV + visibility, exp2(-16.0 * roughness - 1.0)) - 1.0 + visibility);
}

float SingleBounceAO(float visibility)
{
    #ifdef AAAA_GTAO
    #if MULTI_BOUNCE_AMBIENT_OCCLUSION == 1
    return 1.0;
    #else
    return visibility;
    #endif
    #else
    return 1.0;
    #endif
}

float3 GtaoMultiBounce(float visibility, const float3 albedo)
{
    // Jimenez et al. 2016, "Practical Realtime Strategies for Accurate Indirect Occlusion"
    float3 a = 2.0404 * albedo - 0.3324;
    float3 b = -4.7951 * albedo + 0.6417;
    float3 c = 2.7552 * albedo + 0.6903;

    return max(float3(visibility.xxx), ((visibility * a + b) * visibility + c) * visibility);
}

void MultiBounceAO(float visibility, const float3 albedo, inout float3 color)
{
    #if MULTI_BOUNCE_AMBIENT_OCCLUSION
    color *= GtaoMultiBounce(visibility, albedo);
    #endif
}

void MultiBounceSpecularAO(float visibility, const float3 albedo, inout float3 color)
{
    #if MULTI_BOUNCE_AMBIENT_OCCLUSION
    color *= GtaoMultiBounce(visibility, albedo);
    #endif
}

#endif // AAAA_GTAO_INCLUDED