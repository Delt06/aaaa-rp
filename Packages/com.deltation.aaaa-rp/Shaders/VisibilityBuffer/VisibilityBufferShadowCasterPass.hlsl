#ifndef AAAA_VISIBILITY_BUFFER_SHADOW_CASTER_PASS_INCLUDED
#define AAAA_VISIBILITY_BUFFER_SHADOW_CASTER_PASS_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Shadows/ShadowRendering.hlsl"
#include "Packages/com.deltation.aaaa-rp/Shaders/VisibilityBuffer/VisibilityBufferPass.hlsl"

struct ShadowCasterVaryings
{
    float4 positionCS : SV_POSITION;
    #ifdef _ALPHATEST_ON
    VISIBILITY_VALUE_VARYING
    #endif
    EXTRA_VARYINGS
};

ShadowCasterVaryings ShadowCasterVS(const uint svInstanceID : SV_InstanceID, const uint svIndexID : SV_VertexID)
{
    float3            positionWS;
    AAAAInstanceData  instanceData;
    AAAAMeshletVertex vertex;
    const Varyings    varyings = VSBase(svInstanceID, svIndexID, positionWS, instanceData, vertex);

    const float3 lightDirection = GetShadowLightDirection(positionWS);
    const float3 normalWS = TransformObjectToWorldNormal(SafeNormalize(vertex.Normal), instanceData.WorldToObjectMatrix);
    const float  depthBias = ShadowBiases.x;
    const float  normalBias = ShadowBiases.y;
    positionWS = ApplyShadowBias(positionWS, normalWS, lightDirection, depthBias, normalBias);

    ShadowCasterVaryings OUT;
    OUT.positionCS = TransformWorldToHClip(positionWS);

    #ifdef _ALPHATEST_ON
    OUT.visibilityValue = varyings.visibilityValue;
    OUT.uv0 = varyings.uv0;
    #endif

    return OUT;
}

#ifdef _ALPHATEST_ON

void ShadowCasterPS(const ShadowCasterVaryings IN)
{
    const VisibilityBufferValue visibilityBufferValue = UnpackVisibilityBufferValue(IN.visibilityValue);
    AlphaClip(visibilityBufferValue, IN.uv0);
}

#else

void ShadowCasterPS()
{

}

#endif

#endif // AAAA_VISIBILITY_BUFFER_SHADOW_CASTER_PASS_INCLUDED