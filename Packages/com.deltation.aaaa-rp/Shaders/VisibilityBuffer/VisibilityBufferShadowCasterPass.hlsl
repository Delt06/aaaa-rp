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
    const Varyings varyings = VS(svInstanceID, svIndexID);

    ShadowCasterVaryings OUT;
    OUT.positionCS = varyings.positionCS;

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