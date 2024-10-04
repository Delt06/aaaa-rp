#ifndef AAAA_VISIBILITY_BUFFER_SHADOW_CASTER_PASS_INCLUDED
#define AAAA_VISIBILITY_BUFFER_SHADOW_CASTER_PASS_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Shadows/ShadowRendering.hlsl"
#include "Packages/com.deltation.aaaa-rp/Shaders/VisibilityBuffer/VisibilityBufferPass.hlsl"

struct ShadowCasterVaryings
{
    float4 positionCS : SV_POSITION;
};

ShadowCasterVaryings ShadowCasterVS(const uint svInstanceID : SV_InstanceID, const uint svIndexID : SV_VertexID)
{
    const Varyings varyings = VS(svInstanceID, svIndexID);

    ShadowCasterVaryings OUT;
    OUT.positionCS = varyings.positionCS;
    return OUT;
}

void ShadowCasterPS()
{
}

#endif // AAAA_VISIBILITY_BUFFER_SHADOW_CASTER_PASS_INCLUDED