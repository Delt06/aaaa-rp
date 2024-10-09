#ifndef AAAA_GTAO_INCLUDED
#define AAAA_GTAO_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"

Texture2D<uint> _GTAOTerm;
float4          _GTAOResolutionScale;

#if defined(AAAA_GTAO) || defined(AAAA_GTAO_BENT_NORMALS)
#define AAAA_GTAO_ANY 1
#define MULTI_BOUNCE_AMBIENT_OCCLUSION 1
#define DIRECT_LIGHTING_AO_MICROSHADOWS 1
#endif

struct GTAOUtils
{
    static float3 NormalVS_XeGTAOToUnity(float3 normalVS)
    {
        normalVS.yz *= -1;
        return normalVS;
    }

    static float4 R8G8B8A8_UNORM_to_FLOAT4(uint packedInput)
    {
        float4 unpackedOutput;
        unpackedOutput.x = (float)(packedInput & 0x000000ff) / 255;
        unpackedOutput.y = (float)(((packedInput >> 8) & 0x000000ff)) / 255;
        unpackedOutput.z = (float)(((packedInput >> 16) & 0x000000ff)) / 255;
        unpackedOutput.w = (float)(packedInput >> 24) / 255;
        return unpackedOutput;
    }

    // a contact shadow approximation, totally not physically correct; a riff on "Chan 2018, "Material Advances in Call of Duty: WWII" and "The Technical Art of Uncharted 4" http://advances.realtimerendering.com/other/2016/naughty_dog/NaughtyDog_TechArt_Final.pdf (microshadowing)"
    // TODO: figure it out with bent normals! see https://www.activision.com/cdn/research/siggraph_2018_opt.pdf
    static float ComputeMicroShadowing(float NoL, float ao)
    {
        #if DIRECT_LIGHTING_AO_MICROSHADOWS
        #if 0 // from the paper - different from Filament and looks wrong
        float aperture = 2.0 * ao * ao;
        return saturate( abs(NoL) + aperture - 1.0 );
        #else // filament version
        float aperture = rsqrt(1.0000001 - ao);
        NoL += 0.1; // when using bent normals, avoids overshadowing - bent normals are just approximation anyhow
        return saturate(NoL * aperture);
        #endif
        #else
        return 1;
        #endif
    }
};


void DecodeVisibilityBentNormal(const uint packedValue, out float visibility, out float3 bentNormal)
{
    float4 decoded = GTAOUtils::R8G8B8A8_UNORM_to_FLOAT4(packedValue);
    bentNormal = decoded.xyz * 2.0 - 1.0; // could normalize - don't want to since it's done so many times, better to do it at the final step only
    visibility = decoded.w;
}

void SampleGTAO(const uint2 pixelCoords, const float3 normalWS, out float visibility, out float3 bentNormalWS)
{
    visibility = 1;
    bentNormalWS = normalWS;

    #ifdef AAAA_GTAO_ANY
    const uint packedValue = LOAD_TEXTURE2D(_GTAOTerm, pixelCoords * _GTAOResolutionScale.xy).r;
    #ifdef AAAA_GTAO_BENT_NORMALS
    DecodeVisibilityBentNormal(packedValue, visibility, bentNormalWS);
    bentNormalWS = GTAOUtils::NormalVS_XeGTAOToUnity(bentNormalWS);
    bentNormalWS = TransformViewToWorldNormal(bentNormalWS, true);
    #else
    visibility = packedValue / 255.05;
    #endif
    #endif
}


#endif // AAAA_GTAO_INCLUDED