#ifndef AAAA_VISIBILITY_BUFFER_MATERIALS_INCLUDED
#define AAAA_VISIBILITY_BUFFER_MATERIALS_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Bindless.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Barycentric.hlsl"
#include "Packages/com.deltation.aaaa-rp/Runtime/AAAAStructs.cs.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

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

    void AddTilingOffset(const float4 tilingOffset)
    {
        uv = uv * tilingOffset.xy + tilingOffset.zw;
        ddx *= tilingOffset.xy;
        ddy *= tilingOffset.xy;
    }
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
    const uint textureIndex = materialData.AlbedoIndex;

    float4 textureAlbedo;

    UNITY_BRANCH
    if (textureIndex != (uint)NO_TEXTURE_INDEX)
    {
        const Texture2D texture = GetBindlessTexture2D(NonUniformResourceIndex(textureIndex));
        textureAlbedo = SAMPLE_TEXTURE2D_GRAD(texture, sampler_TrilinearRepeat_Aniso16, uv.uv, uv.ddx, uv.ddy);
    }
    else
    {
        textureAlbedo = float4(1, 1, 1, 1);
    }

    return materialData.AlbedoColor * textureAlbedo;
}

float3 SampleNormalTS(const InterpolatedUV uv, const AAAAMaterialData materialData)
{
    const uint textureIndex = materialData.NormalsIndex;

    float3 normalTS;

    UNITY_BRANCH
    if (textureIndex != (uint)NO_TEXTURE_INDEX)
    {
        const Texture2D texture = GetBindlessTexture2D(NonUniformResourceIndex(textureIndex));
        const float4    packedNormal = SAMPLE_TEXTURE2D_GRAD(texture, sampler_TrilinearRepeat_Aniso16, uv.uv, uv.ddx, uv.ddy);
        normalTS = UnpackNormalScale(packedNormal, materialData.NormalsStrength);
    }
    else
    {
        normalTS = float3(0, 0, 1);
    }

    return normalTS;
}

struct MaterialMasks
{
    float roughness;
    float metallic;
};

MaterialMasks SampleMasks(const InterpolatedUV uv, const AAAAMaterialData materialData)
{
    const uint textureIndex = materialData.MasksIndex;

    MaterialMasks materialMasks;

    UNITY_BRANCH
    if (textureIndex != (uint)NO_TEXTURE_INDEX)
    {
        const Texture2D texture = GetBindlessTexture2D(NonUniformResourceIndex(textureIndex));
        const float4    packedMasks = SAMPLE_TEXTURE2D_GRAD(texture, sampler_TrilinearRepeat_Aniso16, uv.uv, uv.ddx, uv.ddy);
        materialMasks.roughness = packedMasks.r;
        materialMasks.metallic = packedMasks.g;
    }
    else
    {
        materialMasks.roughness = 1;
        materialMasks.metallic = 1;
    }

    materialMasks.roughness = saturate(materialMasks.roughness * materialData.Roughness);
    materialMasks.metallic = saturate(materialMasks.metallic * materialData.Metallic);

    return materialMasks;
}

#endif // AAAA_VISIBILITY_BUFFER_MATERIALS_INCLUDED