#ifndef AAAA_SHADOWS_INCLUDED
#define AAAA_SHADOWS_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Bindless.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Shadows/PCF.hlsl"
#include "Packages/com.deltation.aaaa-rp/Runtime/Lighting/AAAAShadowLightSlice.cs.hlsl"

StructuredBuffer<AAAAShadowLightSlice> _ShadowLightSlices;

struct CascadedDirectionalLightShadowSample
{
    float shadowAttenuation;
    float shadowFade;
    float cascadeIndex;
};

float SampleDirectionalLightShadowMap(const uint index, const float4 atlasSize, const float3 shadowCoords, const bool isSoftShadow)
{
    const Texture2D<float>       shadowMap = GetBindlessTexture2DFloat(index);
    const SamplerComparisonState shadowMapSampler = sampler_LinearClampCompare;

    UNITY_BRANCH
    if (isSoftShadow)
    {
        return SampleShadow_PCF_Tent_5x5(atlasSize, shadowCoords, shadowMap, shadowMapSampler);
    }

    return SAMPLE_TEXTURE2D_SHADOW(shadowMap, shadowMapSampler, shadowCoords);
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

float GetLightShadowFade(const float3 positionWS, const float2 shadowFadeParams)
{
    const float3 camToPixel = positionWS - _WorldSpaceCameraPos;
    const float  distanceCamToPixel2 = dot(camToPixel, camToPixel);
    return saturate(distanceCamToPixel2 * shadowFadeParams.x + shadowFadeParams.y);
}

CascadedDirectionalLightShadowSample SampleCascadedDirectionalLightShadow(const float3 positionWS, const float2  sliceRange, const float2 fadeParams,
                                                                          const bool   isSoftShadow, const float shadowStrength = 1)
{
    CascadedDirectionalLightShadowSample shadowAttenuationValue;
    shadowAttenuationValue.shadowFade = 0;
    shadowAttenuationValue.cascadeIndex = -1;
    shadowAttenuationValue.shadowAttenuation = 1;

    UNITY_BRANCH
    if (sliceRange.y > 0)
    {
        AAAAShadowLightSlice cascadeSlices[4];
        LoadCascadeShadowLightSlices(sliceRange, cascadeSlices);

        CascadeSelectionParameters cascadeSelectionParameters;
        cascadeSelectionParameters.Slices = cascadeSlices;
        cascadeSelectionParameters.CascadeCount = sliceRange.y;
        const float                selectedCascadeIndex = ComputeCascadeIndex(positionWS, cascadeSelectionParameters);
        const AAAAShadowLightSlice selectedCascadeSlice = cascadeSlices[selectedCascadeIndex];

        const int bindlessShadowMapIndex = selectedCascadeSlice.BindlessShadowMapIndex;
        if (bindlessShadowMapIndex != -1)
        {
            shadowAttenuationValue.cascadeIndex = selectedCascadeIndex;

            const bool   isPerspective = false;
            const float3 shadowCoords = TransformWorldToShadowCoords(positionWS, selectedCascadeSlice.WorldToShadowCoords, isPerspective);
            shadowAttenuationValue.shadowAttenuation = SampleDirectionalLightShadowMap(NonUniformResourceIndex(bindlessShadowMapIndex),
                                                                                       selectedCascadeSlice.AtlasSize, shadowCoords, isSoftShadow);

            shadowAttenuationValue.shadowFade = GetLightShadowFade(positionWS, fadeParams);
            shadowAttenuationValue.shadowAttenuation = lerp(1, shadowAttenuationValue.shadowAttenuation, shadowStrength);
            shadowAttenuationValue.shadowAttenuation = lerp(shadowAttenuationValue.shadowAttenuation, 1, shadowAttenuationValue.shadowFade);
        }
    }

    return shadowAttenuationValue;
}

#endif // AAAA_SHADOWS_INCLUDED