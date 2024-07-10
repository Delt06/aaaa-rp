#ifndef AAAA_GBUFFER_INCLUDED
#define AAAA_GBUFFER_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

struct GBufferOutput
{
    float4 albedo : SV_Target0;
    float4 normalsWS : SV_Target1;
};

GBufferOutput ConstructGBufferOutput(float3 albedo, float3 normalsWS)
{
    GBufferOutput output;
    output.albedo = float4(albedo, 1.0f);
    output.normalsWS = float4(normalsWS * 0.5 + 0.5, 0.0f);
    return output;
}

#endif // AAAA_GBUFFER_INCLUDED