#ifndef AAAA_GTAO_INCLUDED
#define AAAA_GTAO_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"

Texture2D<uint> _GTAOTerm;
float4          _GTAOResolutionScale;

#if defined(AAAA_GTAO) || defined(AAAA_GTAO_BENT_NORMALS)
#define AAAA_GTAO_ANY 1
#define MULTI_BOUNCE_AMBIENT_OCCLUSION 1
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
    bentNormalWS = normalize(lerp(bentNormalWS, normalWS, visibility * visibility));
    #else
    visibility = packedValue / 255.05;
    #endif
    #endif
}


#endif // AAAA_GTAO_INCLUDED