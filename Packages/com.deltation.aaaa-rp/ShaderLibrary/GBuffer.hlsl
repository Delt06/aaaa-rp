#ifndef AAAA_GBUFFER_INCLUDED
#define AAAA_GBUFFER_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

TEXTURE2D(_GBuffer_Albedo);
SAMPLER(sampler_GBuffer_Albedo);
TEXTURE2D(_GBuffer_Normals);
SAMPLER(sampler_GBuffer_Normals);

struct GBufferOutput
{
    float4 albedo : SV_Target0;
    float2 packedNormalWS : SV_Target1;
};

struct GBufferValue
{
    float3 albedo;
    float3 normalWS;
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
    return output;
}

GBufferValue SampleGBuffer(const float2 screenUV)
{
    GBufferValue output;

    output.albedo = SAMPLE_TEXTURE2D_LOD(_GBuffer_Albedo, sampler_GBuffer_Albedo, screenUV, 0).rgb;
    output.normalWS = UnpackGBufferNormal(SAMPLE_TEXTURE2D_LOD(_GBuffer_Normals, sampler_GBuffer_Normals, screenUV, 0).xy);

    return output;
}

#endif // AAAA_GBUFFER_INCLUDED