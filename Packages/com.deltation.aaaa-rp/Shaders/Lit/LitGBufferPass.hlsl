#ifndef AAAA_LIT_GBUFFER_PASS_INCLUDED
#define AAAA_LIT_GBUFFER_PASS_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/GBuffer.hlsl"

#include "Packages/com.deltation.aaaa-rp/Shaders/Lit/LitInput.hlsl"

struct AppData
{
    float3 vertexOS : POSITION;
    float3 normalOS : NORMAL;
    float4 texcoord0 : TEXCOORD0;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 normalWS : NORMAL_WS;
};

Varyings VS(const AppData IN)
{
    Varyings OUT;

    OUT.positionCS = TransformObjectToHClip(IN.vertexOS);
    OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);

    return  OUT;
}

GBufferOutput PS(const Varyings IN)
{
    GBufferValue gbufferValue;
    gbufferValue.albedo = 1;
    gbufferValue.metallic = 0;
    gbufferValue.roughness = 0.5;
    gbufferValue.normalWS = SafeNormalize(IN.normalWS);
    gbufferValue.materialFlags = AAAAMATERIALFLAGS_NONE;
    return PackGBufferOutput(gbufferValue);
}

#endif // AAAA_LIT_GBUFFER_PASS_INCLUDED