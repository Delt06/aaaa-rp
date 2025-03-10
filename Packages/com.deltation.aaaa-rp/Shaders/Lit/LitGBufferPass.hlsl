﻿#ifndef AAAA_LIT_GBUFFER_PASS_INCLUDED
#define AAAA_LIT_GBUFFER_PASS_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/GBuffer.hlsl"

#include "Packages/com.deltation.aaaa-rp/Shaders/Lit/LitInput.hlsl"

struct Attributes
{
    float3 vertexOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float4 texcoord0 : TEXCOORD0;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 normalWS : NORMAL_WS;
    float4 tangentWS : TANGENT_WS;
};

Varyings VS(const Attributes IN)
{
    Varyings OUT;

    OUT.positionCS = TransformObjectToHClip(IN.vertexOS);
    OUT.uv = TransformBaseMapUV(IN.texcoord0.xy);
    OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
    OUT.tangentWS = float4(TransformObjectToWorldDir(IN.tangentOS.xyz), IN.tangentOS.w);

    return OUT;
}

GBufferOutput PS(const Varyings IN, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC)
{
    SurfaceData surfaceData = (SurfaceData)0;
    InitSurfaceData(surfaceData, IN.uv, IN.normalWS, IN.tangentWS, face);
    AlphaClip(surfaceData);

    GBufferValue gbufferValue;
    gbufferValue.albedo = surfaceData.albedo;
    gbufferValue.emission = surfaceData.emission;
    gbufferValue.metallic = surfaceData.metallic;
    gbufferValue.roughness = surfaceData.roughness;
    gbufferValue.normalWS = surfaceData.normalWS;
    gbufferValue.materialFlags = AAAAMATERIALFLAGS_NONE;
    return PackGBufferOutput(gbufferValue);
}

#endif // AAAA_LIT_GBUFFER_PASS_INCLUDED