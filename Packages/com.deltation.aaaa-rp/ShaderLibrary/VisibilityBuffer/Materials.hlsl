#ifndef AAAA_VISIBILITY_BUFFER_MATERIALS_INCLUDED
#define AAAA_VISIBILITY_BUFFER_MATERIALS_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Barycentric.hlsl"
#include "Packages/com.deltation.aaaa-rp/Runtime/AAAAStructs.cs.hlsl"

TEXTURE2D_ARRAY(_SharedAlbedoTextureArray);
SAMPLER(sampler_SharedAlbedoTextureArray);

StructuredBuffer<AAAAMaterialData> _MaterialData;

AAAAMaterialData PullMaterialData(const uint materialIndex)
{
    return _MaterialData[materialIndex];
}

struct InterpolatedUV
{
    float2 uv;
    float2 ddx;
    float2 ddy;
};

InterpolatedUV InterpolateUV(const BarycentricDerivatives barycentric, const AAAAMeshletVertex v0, const AAAAMeshletVertex v1,
                             const AAAAMeshletVertex      v2)
{
    const float3 u = InterpolateWithBarycentric(barycentric, v0.UV.x, v1.UV.x, v2.UV.x);
    const float3 v = InterpolateWithBarycentric(barycentric, v0.UV.y, v1.UV.y, v2.UV.y);

    InterpolatedUV uv;
    uv.uv = float2(u.x, v.x);
    uv.ddx = float2(u.y, v.y);
    uv.ddy = float2(u.z, v.z);
    return uv;
}

float4 SampleAlbedo(const InterpolatedUV uv, const AAAAMaterialData materialData)
{
    const float4 textureAlbedo = SAMPLE_TEXTURE2D_ARRAY_GRAD(_SharedAlbedoTextureArray, sampler_SharedAlbedoTextureArray, uv.uv,
                                                             materialData.AlbedoIndex, uv.ddx, uv.ddy);
    return materialData.AlbedoColor * textureAlbedo;
}


#endif // AAAA_VISIBILITY_BUFFER_MATERIALS_INCLUDED