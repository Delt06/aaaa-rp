#ifndef AAAA_LIT_META_PASS_INCLUDED
#define AAAA_LIT_META_PASS_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/GBuffer.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/MetaInput.hlsl"

#include "Packages/com.deltation.aaaa-rp/Shaders/Lit/LitInput.hlsl"

Varyings VS(const Attributes IN)
{
    Attributes attributes = IN;
    attributes.uv0 = TransformBaseMapUV(IN.uv0);

    return MetaVS(attributes);
}

float4 PS(const Varyings IN, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_TARGET
{
    const float3 normalWS = 0;
    const float4 tangentWS = 0;
    SurfaceData  surfaceData = (SurfaceData)0;
    InitSurfaceData(surfaceData, IN.uv, normalWS, tangentWS, face);
    AlphaClip(surfaceData);

    MetaInput metaInput;
    metaInput.Albedo = surfaceData.albedo;
    metaInput.Emission = surfaceData.emission;

    return MetaPS(IN, metaInput);
}

#endif // AAAA_LIT_META_PASS_INCLUDED