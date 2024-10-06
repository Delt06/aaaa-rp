#ifndef AAAA_SHADOWS_INCLUDED
#define AAAA_SHADOWS_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Bindless.hlsl"
#include "Packages/com.deltation.aaaa-rp/Runtime/Lighting/AAAAShadowLightSlice.cs.hlsl"

StructuredBuffer<AAAAShadowLightSlice> _ShadowLightSlices;

float SampleDirectionalLightShadowMap(const float3 shadowCoords, const uint index)
{
    Texture2D<float> shadowMap = GetBindlessTexture2DFloat(index);
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

struct CascadeSelectionParameters
{
    AAAAShadowLightSlice Slices[4];
    float                CascadeCount;
};

void LoadCascadeShadowLightSlices(const float2 sliceRange, out AAAAShadowLightSlice slices[4])
{
    AAAAShadowLightSlice fallbackLightSlice = (AAAAShadowLightSlice)0;
    fallbackLightSlice.BoundingSphere = float4(0, 0, 0, FLT_INF);
    fallbackLightSlice.BindlessShadowMapIndex = -1;

    slices[0] = _ShadowLightSlices[sliceRange.x];

    UNITY_UNROLL
    for (int i = 1; i < 4; i++)
    {
        if (i < sliceRange.y)
        {
            slices[i] = _ShadowLightSlices[sliceRange.x + i];
        }
        else
        {
            slices[i] = fallbackLightSlice;
        }
    }
}

float ComputeCascadeIndex(const float3 positionWS, const CascadeSelectionParameters parameters)
{
    float3 fromCenter0 = positionWS - parameters.Slices[0].BoundingSphere.xyz;
    float3 fromCenter1 = positionWS - parameters.Slices[1].BoundingSphere.xyz;
    float3 fromCenter2 = positionWS - parameters.Slices[2].BoundingSphere.xyz;
    float3 fromCenter3 = positionWS - parameters.Slices[3].BoundingSphere.xyz;
    float4 distances2 = float4(dot(fromCenter0, fromCenter0),
                               dot(fromCenter1, fromCenter1),
                               dot(fromCenter2, fromCenter2),
                               dot(fromCenter3, fromCenter3));

    const float4 radii = float4(parameters.Slices[0].BoundingSphere.w,
                                parameters.Slices[1].BoundingSphere.w,
                                parameters.Slices[2].BoundingSphere.w,
                                parameters.Slices[3].BoundingSphere.w);

    float4 weights = half4(distances2 < radii);
    weights.yzw = saturate(weights.yzw - weights.xyz);

    return min(4.0 - dot(weights, float4(4, 3, 2, 1)), parameters.CascadeCount - 1);
}

#endif // AAAA_SHADOWS_INCLUDED