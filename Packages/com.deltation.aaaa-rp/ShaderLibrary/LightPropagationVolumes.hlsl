#ifndef AAAA_LIGHT_PROPAGATION_VOLUMES_INCLUDED
#define AAAA_LIGHT_PROPAGATION_VOLUMES_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

struct RsmOutput
{
    float3 positionWS : SV_Target0;
    float2 packedNormalWS : SV_Target1;
    float3 flux : SV_Target2;
};

struct RsmValue
{
    float3 positionWS;
    float3 normalWS;
    float3 flux;
};

float2 PackRsmNormal(const float3 normal)
{
    return PackNormalOctQuadEncode(normal);
}

float3 UnpackRsmNormal(const float2 packedNormal)
{
    return UnpackNormalOctQuadEncode(packedNormal);
}

RsmOutput PackRsmOutput(const RsmValue value)
{
    RsmOutput output;
    output.positionWS = value.positionWS;
    output.packedNormalWS = PackRsmNormal(value.normalWS);
    output.flux = value.flux;
    return output;
}

#endif // AAAA_LIGHT_PROPAGATION_VOLUMES_INCLUDED