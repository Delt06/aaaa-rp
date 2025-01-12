#ifndef AAAA_VISIBILITY_BUFFER_SHADOW_CASTER_PASS_INCLUDED
#define AAAA_VISIBILITY_BUFFER_SHADOW_CASTER_PASS_INCLUDED

#if defined(_ALPHATEST_ON)
#define REQUIRE_VISIBILITY_VALUE_INTERPOLATOR
#endif

#if defined(_ALPHATEST_ON)
#define REQUIRE_UV0_INTERPOLATOR
#endif

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Shadows/ShadowRendering.hlsl"
#include "Packages/com.deltation.aaaa-rp/Shaders/VisibilityBuffer/VisibilityBufferPass.hlsl"

struct ShadowCasterVaryings
{
    float4 positionCS : SV_POSITION;

    #ifdef REQUIRE_VISIBILITY_VALUE_INTERPOLATOR
    VISIBILITY_VALUE_VARYING
    #endif

    #ifdef REQUIRE_UV0_INTERPOLATOR
    float2 uv0 : TEXCOORD0;
    #endif
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

    #ifdef REQUIRE_VISIBILITY_VALUE_INTERPOLATOR
    OUT.visibilityValue = varyings.visibilityValue;
    #endif

    #ifdef REQUIRE_UV0_INTERPOLATOR
    OUT.uv0 = varyings.uv0;
    #endif

    return OUT;
}

void ShadowCasterPS(const ShadowCasterVaryings IN)
{
    float4 albedo;

    #if defined(REQUIRE_VISIBILITY_VALUE_INTERPOLATOR) && defined(REQUIRE_UV0_INTERPOLATOR)
    const VisibilityBufferValue visibilityBufferValue = UnpackVisibilityBufferValue(IN.visibilityValue);
    const AAAAInstanceData      instanceData = PullInstanceData(visibilityBufferValue.instanceID);
    const AAAAMaterialData      materialData = PullMaterialData(instanceData.MaterialIndex);
    albedo = SampleAlbedo(IN.uv0, materialData);
    #else
    albedo = 0;
    #endif

    #if defined(_ALPHATEST_ON)
    AlphaClip(materialData, albedo);
    #endif
}

#endif // AAAA_VISIBILITY_BUFFER_SHADOW_CASTER_PASS_INCLUDED