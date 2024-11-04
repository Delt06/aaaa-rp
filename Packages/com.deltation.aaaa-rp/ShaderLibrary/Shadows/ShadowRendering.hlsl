#ifndef AAAA_SHADOW_RENDERING_INCLUDED
#define AAAA_SHADOW_RENDERING_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.deltation.aaaa-rp/Runtime/Lighting/AAAAShadowRenderingConstantBuffer.cs.hlsl"

#define UNITY_MATRIX_P (ShadowProjectionMatrix)
#define UNITY_MATRIX_V (ShadowViewMatrix)
#define UNITY_MATRIX_VP (ShadowViewProjection)

float3 GetShadowLightDirection(const float3 positionWS)
{
    return SafeNormalize(ShadowLightDirection.xyz - positionWS * ShadowLightDirection.w);
}

float3 ApplyShadowBias(float3 positionWS, const float3 normalWS, const float3 lightDirection, const float depthBias, const float normalBias)
{
    const float invNdotL = 1.0 - saturate(dot(lightDirection, normalWS));
    const float normalBiasScale = invNdotL * normalBias;

    positionWS = lightDirection * depthBias.xxx + positionWS;
    positionWS = normalWS * normalBiasScale.xxx + positionWS;
    return positionWS;
}

#endif // AAAA_SHADOW_RENDERING_INCLUDED