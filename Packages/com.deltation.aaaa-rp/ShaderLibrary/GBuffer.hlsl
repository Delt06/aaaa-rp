#ifndef AAAA_GBUFFER_INCLUDED
#define AAAA_GBUFFER_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

TEXTURE2D(_GBuffer_Albedo);
SAMPLER(sampler_GBuffer_Albedo);
TEXTURE2D(_GBuffer_Normals);
SAMPLER(sampler_GBuffer_Normals);
TEXTURE2D(_GBuffer_Masks);
SAMPLER(sampler_GBuffer_Masks);

struct GBufferOutput
{
    float4 albedo : SV_Target0;
    float2 packedNormalWS : SV_Target1;
    float4 masks : SV_Target2;
};

struct GBufferValue
{
    float3 albedo;
    float3 normalWS;
    float  roughness;
    float  metallic;
    uint   materialFlags;
};

float2 PackGBufferNormal(const float3 normal)
{
    return PackNormalOctQuadEncode(normal);
}

float3 UnpackGBufferNormal(const float2 packedNormal)
{
    return UnpackNormalOctQuadEncode(packedNormal);
}

GBufferOutput PackGBufferOutput(const GBufferValue value)
{
    GBufferOutput output;
    output.albedo = float4(value.albedo, 1.0f);
    output.packedNormalWS = PackGBufferNormal(value.normalWS);
    output.masks = float4(value.roughness, value.metallic, 0, PackByte(value.materialFlags));
    return output;
}

GBufferValue SampleGBuffer(const float2 screenUV)
{
    GBufferValue output;

    output.albedo = SAMPLE_TEXTURE2D_LOD(_GBuffer_Albedo, sampler_GBuffer_Albedo, screenUV, 0).rgb;
    output.normalWS = UnpackGBufferNormal(SAMPLE_TEXTURE2D_LOD(_GBuffer_Normals, sampler_GBuffer_Normals, screenUV, 0).xy);

    const float4 masks = SAMPLE_TEXTURE2D_LOD(_GBuffer_Masks, sampler_GBuffer_Masks, screenUV, 0);
    output.roughness = masks.r;
    output.metallic = masks.g;
    output.materialFlags = UnpackByte(masks.a);

    return output;
}

#endif // AAAA_GBUFFER_INCLUDED