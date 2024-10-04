#ifndef AAAA_SHADOWS_INCLUDED
#define AAAA_SHADOWS_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"

TEXTURE2D_ARRAY_SHADOW(_DirectionalLightShadowMapArray);

float SampleDirectionalLightShadowMap(const float3 shadowCoords, const uint index)
{
    return SAMPLE_TEXTURE2D_ARRAY_SHADOW(_DirectionalLightShadowMapArray, sampler_LinearClampCompare, shadowCoords, index);
}

float3 TransformWorldToShadowCoords(const float3 positionWS, const float4x4 worldToShadowCoordsMatrix, const bool isPerspective)
{
    float4 shadowCoords = mul(worldToShadowCoordsMatrix, float4(positionWS, 1.0f));

    if (isPerspective)
    {
        shadowCoords.xyz /= shadowCoords.w;
    }

    return shadowCoords.xyz;
}

#endif // AAAA_SHADOWS_INCLUDED