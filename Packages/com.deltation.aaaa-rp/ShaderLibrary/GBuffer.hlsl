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
    float4 normalsWS : SV_Target1;
};

float3 PackGBufferNormal(const float3 normal)
{
    return normal * 0.5 + 0.5;
}

float3 UnpackGBufferNormal(const float3 packedNormal)
{
    return packedNormal * 2 - 1;
}

GBufferOutput ConstructGBufferOutput(const float3 albedo, const float3 normalsWS)
{
    GBufferOutput output;
    output.albedo = float4(albedo, 1.0f);
    output.normalsWS = float4(PackGBufferNormal(normalsWS), 0.0f);
    return output;
}

GBufferOutput SampleGBuffer(const float2 screenUV)
{
    GBufferOutput output;

    output.albedo = SAMPLE_TEXTURE2D_LOD(_GBuffer_Albedo, sampler_GBuffer_Albedo, screenUV, 0);

    output.normalsWS = SAMPLE_TEXTURE2D_LOD(_GBuffer_Normals, sampler_GBuffer_Normals, screenUV, 0);
    output.normalsWS.xyz = UnpackGBufferNormal(output.normalsWS.xyz);

    return output;
}

#endif // AAAA_GBUFFER_INCLUDED