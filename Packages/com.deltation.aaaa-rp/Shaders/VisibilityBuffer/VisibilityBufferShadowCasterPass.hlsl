#ifndef AAAA_VISIBILITY_BUFFER_SHADOW_CASTER_PASS_INCLUDED
#define AAAA_VISIBILITY_BUFFER_SHADOW_CASTER_PASS_INCLUDED

#if defined(_ALPHATEST_ON) || defined(AAAA_LPV_REFLECTIVE_SHADOW_MAPS)
#define REQUIRE_VISIBILITY_VALUE_INTERPOLATOR
#endif

#if defined(_ALPHATEST_ON) || defined(AAAA_LPV_REFLECTIVE_SHADOW_MAPS)
#define REQUIRE_UV0_INTERPOLATOR
#endif

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Shadows/ShadowRendering.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/LightPropagationVolumes.hlsl"
#include "Packages/com.deltation.aaaa-rp/Shaders/VisibilityBuffer/VisibilityBufferPass.hlsl"

struct ShadowCasterVaryings
{
    float4 positionCS : SV_POSITION;

    #ifdef REQUIRE_VISIBILITY_VALUE_INTERPOLATOR
    VISIBILITY_VALUE_VARYING
    #endif

    #ifdef AAAA_LPV_REFLECTIVE_SHADOW_MAPS
    float3 positionWS : POSITION_WS;
    float3 normalWS : NORMAL_WS;
    #endif

    #ifdef REQUIRE_UV0_INTERPOLATOR
    float2 uv0 : TEXCOORD0;
    #endif
};

struct DummyOutput
{
};

#ifdef AAAA_LPV_REFLECTIVE_SHADOW_MAPS
#define SHADOW_CASTER_FRAGMENT_OUTPUT RsmOutput
#else
#define SHADOW_CASTER_FRAGMENT_OUTPUT DummyOutput
#endif

void TransferOutput(const ShadowCasterVaryings IN, const float4 albedo, const float4 emission, inout SHADOW_CASTER_FRAGMENT_OUTPUT OUT)
{
    #ifdef AAAA_LPV_REFLECTIVE_SHADOW_MAPS
    RsmValue rsmValue;
    rsmValue.positionWS = IN.positionWS;
    rsmValue.normalWS = SafeNormalize(IN.normalWS);
    rsmValue.flux = (1 + emission.rgb) * albedo.rgb;
    OUT = PackRsmOutput(rsmValue);
    #endif
}

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

    #ifdef AAAA_LPV_REFLECTIVE_SHADOW_MAPS
    OUT.positionWS = positionWS;
    OUT.normalWS = normalWS;
    #endif

    return OUT;
}

SHADOW_CASTER_FRAGMENT_OUTPUT ShadowCasterPS(const ShadowCasterVaryings IN)
{
    SHADOW_CASTER_FRAGMENT_OUTPUT OUT = (SHADOW_CASTER_FRAGMENT_OUTPUT)0;

    float4 albedo;
    float4 emission;

    #if defined(REQUIRE_VISIBILITY_VALUE_INTERPOLATOR) && defined(REQUIRE_UV0_INTERPOLATOR)
    const VisibilityBufferValue visibilityBufferValue = UnpackVisibilityBufferValue(IN.visibilityValue);
    const AAAAInstanceData      instanceData = PullInstanceData(visibilityBufferValue.instanceID);
    const AAAAMaterialData      materialData = PullMaterialData(instanceData.MaterialIndex);
    albedo = SampleAlbedo(IN.uv0, materialData);
    emission = materialData.Emission;
    #else
    albedo = 0;
    emission = 0;
    #endif

    #if defined(_ALPHATEST_ON)
    AlphaClip(materialData, albedo);
    #endif

    TransferOutput(IN, albedo, emission, OUT);

    return OUT;
}

#endif // AAAA_VISIBILITY_BUFFER_SHADOW_CASTER_PASS_INCLUDED