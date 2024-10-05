#ifndef AAAA_SHADOWS_INCLUDED
#define AAAA_SHADOWS_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Bindless.hlsl"
#include "Packages/com.deltation.aaaa-rp/Runtime/Lighting/AAAAShadowLightSlice.cs.hlsl"

StructuredBuffer<AAAAShadowLightSlice> _ShadowLightSlices;

float SampleDirectionalLightShadowMap(const float3 shadowCoords, const uint index)
{
    Texture2D<float> shadowMap = GetBindlessTexture2DFloat(NonUniformResourceIndex(index));
    return SAMPLE_TEXTURE2D_SHADOW(shadowMap, sampler_LinearClampCompare, shadowCoords);
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