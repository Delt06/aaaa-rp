#ifndef AAAA_GBUFFER_INCLUDED
#define AAAA_GBUFFER_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

TEXTURE2D(_GBuffer_Albedo);
float4 _GBuffer_Albedo_TexelSize;
TEXTURE2D(_GBuffer_Normals);
TEXTURE2D(_GBuffer_Masks);

#define GBUFFER_SAMPLER (sampler_PointClamp)

struct GBufferOutput
{
    float4 albedo : SV_Target0;
    float2 packedNormalWS : SV_Target1;
    float4 masks : SV_Target2;
    float4 emission : SV_Target3;
};

struct GBufferValue
{
    float3 albedo;
    float3 emission;
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
    output.emission = float4(value.emission, 1.0f);
    output.packedNormalWS = PackGBufferNormal(value.normalWS);
    output.masks = float4(value.roughness, value.metallic, 0, PackByte(value.materialFlags));
    return output;
}

GBufferValue SampleGBuffer(const float2 screenUV)
{
    GBufferValue output;

    output.albedo = SAMPLE_TEXTURE2D_LOD(_GBuffer_Albedo, GBUFFER_SAMPLER, screenUV, 0).rgb;
    output.emission = 0;
    output.normalWS = UnpackGBufferNormal(SAMPLE_TEXTURE2D_LOD(_GBuffer_Normals, GBUFFER_SAMPLER, screenUV, 0).xy);

    const float4 masks = SAMPLE_TEXTURE2D_LOD(_GBuffer_Masks, GBUFFER_SAMPLER, screenUV, 0);
    output.roughness = masks.r;
    output.metallic = masks.g;
    output.materialFlags = UnpackByte(masks.a);

    return output;
}

void GatherGBufferRGBA(const float2 screenUV, TEXTURE2D_PARAM(tex, texSampler), out float4 results[4])
{
    float4 red = GATHER_RED_TEXTURE2D(tex, texSampler, screenUV);
    float4 green = GATHER_GREEN_TEXTURE2D(tex, texSampler, screenUV);
    float4 blue = GATHER_BLUE_TEXTURE2D(tex, texSampler, screenUV);
    float4 alpha = GATHER_ALPHA_TEXTURE2D(tex, texSampler, screenUV);

    results[0] = float4(red[0], green[0], blue[0], alpha[0]);
    results[1] = float4(red[1], green[1], blue[1], alpha[1]);
    results[2] = float4(red[2], green[2], blue[2], alpha[2]);
    results[3] = float4(red[3], green[3], blue[3], alpha[3]);
}

void GatherGBufferLinearRG(const float2 screenUV, TEXTURE2D_PARAM(tex, texSampler), out float2 results[4])
{
    float4 red = GATHER_RED_TEXTURE2D(tex, texSampler, screenUV);
    float4 green = GATHER_GREEN_TEXTURE2D(tex, texSampler, screenUV);

    results[0] = float2(red[0], green[0]);
    results[1] = float2(red[1], green[1]);
    results[2] = float2(red[2], green[2]);
    results[3] = float2(red[3], green[3]);
}

GBufferValue SampleGBufferLinear(const float2 screenUV)
{
    GBufferValue output;

    output.albedo = SAMPLE_TEXTURE2D_LOD(_GBuffer_Albedo, sampler_LinearClamp, screenUV, 0).rgb;
    output.emission = 0;

    float2 packedNormals[4];
    GatherGBufferLinearRG(screenUV, TEXTURE2D_ARGS(_GBuffer_Normals, GBUFFER_SAMPLER), packedNormals);
    output.normalWS = SafeNormalize(
        UnpackGBufferNormal(packedNormals[0]) + UnpackGBufferNormal(packedNormals[1]) +
        UnpackGBufferNormal(packedNormals[2]) + UnpackGBufferNormal(packedNormals[3]));

    const float4 masks = SAMPLE_TEXTURE2D_LOD(_GBuffer_Masks, sampler_LinearClamp, screenUV, 0);
    output.roughness = masks.r;
    output.metallic = masks.g;

    const float4 masksA = GATHER_ALPHA_TEXTURE2D(_GBuffer_Masks, GBUFFER_SAMPLER, screenUV);
    output.materialFlags = UnpackByte(masksA[0]) | UnpackByte(masksA[1]) | UnpackByte(masksA[2]) | UnpackByte(masksA[3]);

    return output;
}

#endif // AAAA_GBUFFER_INCLUDED