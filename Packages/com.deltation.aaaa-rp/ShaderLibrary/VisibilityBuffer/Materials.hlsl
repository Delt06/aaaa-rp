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

#define MATERIAL_DEFAULT_SAMPLER (sampler_TrilinearRepeat_Aniso16)

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

float4 SampleAlbedo(const float2 uv, const AAAAMaterialData materialData)
{
    const uint textureIndex = materialData.AlbedoIndex;

    float4 textureAlbedo;

    UNITY_BRANCH
    if (textureIndex != (uint)NO_TEXTURE_INDEX)
    {
        const Texture2D texture = GetBindlessTexture2D(NonUniformResourceIndex(textureIndex));
        textureAlbedo = SAMPLE_TEXTURE2D(texture, MATERIAL_DEFAULT_SAMPLER, uv);
    }
    else
    {
        textureAlbedo = float4(1, 1, 1, 1);
    }

    return materialData.AlbedoColor * textureAlbedo;
}

float4 SampleAlbedoLOD(const float2 uv, const AAAAMaterialData materialData, const float lod)
{
    const uint textureIndex = materialData.AlbedoIndex;

    float4 textureAlbedo;

    UNITY_BRANCH
    if (textureIndex != (uint)NO_TEXTURE_INDEX)
    {
        const Texture2D texture = GetBindlessTexture2D(NonUniformResourceIndex(textureIndex));
        textureAlbedo = SAMPLE_TEXTURE2D_LOD(texture, MATERIAL_DEFAULT_SAMPLER, uv, lod);
    }
    else
    {
        textureAlbedo = float4(1, 1, 1, 1);
    }

    return materialData.AlbedoColor * textureAlbedo;
}

float4 SampleAlbedoTextureGrad(const InterpolatedUV uv, const AAAAMaterialData materialData)
{
    const uint textureIndex = materialData.AlbedoIndex;

    float4 textureAlbedo;

    UNITY_BRANCH
    if (textureIndex != (uint)NO_TEXTURE_INDEX)
    {
        const Texture2D texture = GetBindlessTexture2D(NonUniformResourceIndex(textureIndex));
        textureAlbedo = SAMPLE_TEXTURE2D_GRAD(texture, MATERIAL_DEFAULT_SAMPLER, uv.uv, uv.ddx, uv.ddy);
    }
    else
    {
        textureAlbedo = float4(1, 1, 1, 1);
    }

    return textureAlbedo;
}

float3 SampleNormalTSGrad(const InterpolatedUV uv, const AAAAMaterialData materialData)
{
    const uint textureIndex = materialData.NormalsIndex;

    float3 normalTS;

    UNITY_BRANCH
    if (textureIndex != (uint)NO_TEXTURE_INDEX)
    {
        const Texture2D texture = GetBindlessTexture2D(NonUniformResourceIndex(textureIndex));
        const float4    packedNormal = SAMPLE_TEXTURE2D_GRAD(texture, MATERIAL_DEFAULT_SAMPLER, uv.uv, uv.ddx, uv.ddy);
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

MaterialMasks SampleMasks(const float2 uv, const AAAAMaterialData materialData)
{
    const uint textureIndex = materialData.MasksIndex;

    MaterialMasks materialMasks;

    UNITY_BRANCH
    if (textureIndex != (uint)NO_TEXTURE_INDEX)
    {
        const Texture2D texture = GetBindlessTexture2D(NonUniformResourceIndex(textureIndex));
        const float4    packedMasks = SAMPLE_TEXTURE2D(texture, MATERIAL_DEFAULT_SAMPLER, uv);
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

MaterialMasks SampleMasksLOD(const float2 uv, const AAAAMaterialData materialData, const float lod)
{
    const uint textureIndex = materialData.MasksIndex;

    MaterialMasks materialMasks;

    UNITY_BRANCH
    if (textureIndex != (uint)NO_TEXTURE_INDEX)
    {
        const Texture2D texture = GetBindlessTexture2D(NonUniformResourceIndex(textureIndex));
        const float4    packedMasks = SAMPLE_TEXTURE2D_LOD(texture, MATERIAL_DEFAULT_SAMPLER, uv, lod);
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

MaterialMasks SampleMasksGrad(const InterpolatedUV uv, const AAAAMaterialData materialData)
{
    const uint textureIndex = materialData.MasksIndex;

    MaterialMasks materialMasks;

    UNITY_BRANCH
    if (textureIndex != (uint)NO_TEXTURE_INDEX)
    {
        const Texture2D texture = GetBindlessTexture2D(NonUniformResourceIndex(textureIndex));
        const float4    packedMasks = SAMPLE_TEXTURE2D_GRAD(texture, MATERIAL_DEFAULT_SAMPLER, uv.uv, uv.ddx, uv.ddy);
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

// https://github.com/Unity-Technologies/Graphics/blob/e42df452b62857a60944aed34f02efa1bda50018/Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl#L218
// Return modified perceptualRoughness based on provided variance (get from GeometricNormalVariance + TextureNormalVariance)
float NormalFiltering(float perceptualRoughness, float variance, float threshold)
{
    const float roughness = perceptualRoughness * perceptualRoughness;
    // Ref: Geometry into Shading - http://graphics.pixar.com/library/BumpRoughness/paper.pdf - equation (3)
    float squaredRoughness = saturate(roughness * roughness + min(2.0 * variance, threshold * threshold));
    // threshold can be really low, square the value for easier control
    return sqrt(sqrt(squaredRoughness));
}

// Reference: Error Reduction and Simplification for Shading Anti-Aliasing
// Specular antialiasing for geometry-induced normal (and NDF) variations: Tokuyoshi / Kaplanyan et al.'s method.
// This is the deferred approximation, which works reasonably well so we keep it for forward too for now.
// screenSpaceVariance should be at most 0.5^2 = 0.25, as that corresponds to considering
// a gaussian pixel reconstruction kernel with a standard deviation of 0.5 of a pixel, thus 2 sigma covering the whole pixel.
float GeometricNormalVariance(BarycentricDerivatives geometricNormalWS, float screenSpaceVariance)
{
    const float3 deltaU = geometricNormalWS.ddx;
    const float3 deltaV = geometricNormalWS.ddy;
    return screenSpaceVariance * (dot(deltaU, deltaU) + dot(deltaV, deltaV));
}

// Return modified perceptualRoughness
float GeometricNormalFiltering(float perceptualRoughness, BarycentricDerivatives geometricNormalWS, float screenSpaceVariance,
                               float threshold)
{
    float variance = GeometricNormalVariance(geometricNormalWS, screenSpaceVariance);
    return NormalFiltering(perceptualRoughness, variance, threshold);
}

#endif // AAAA_VISIBILITY_BUFFER_MATERIALS_INCLUDED