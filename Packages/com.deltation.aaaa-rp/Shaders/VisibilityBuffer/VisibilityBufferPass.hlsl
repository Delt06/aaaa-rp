#ifndef AAAA_VISIBILITY_BUFFER_PASS_INCLUDED
#define AAAA_VISIBILITY_BUFFER_PASS_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Instances.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Materials.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Meshlets.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Utils.hlsl"

#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawArgs
#include "UnityIndirect.cginc"

ByteAddressBuffer _MeshletRenderRequests;

#define VISIBILITY_VALUE_VARYING nointerpolation uint2 visibilityValue : VISIBILITY_VALUE;
#define EXTRA_VARYINGS

#ifdef _ALPHATEST_ON
#undef EXTRA_VARYINGS
#define EXTRA_VARYINGS float2 uv0 : TEXCOORD0;
#endif

struct Varyings
{
    float4                positionCS : SV_POSITION;
    VISIBILITY_VALUE_VARYING
    EXTRA_VARYINGS
};

void WriteUV(const AAAAMeshletVertex vertex, const AAAAMaterialData materialData, out float2 uv)
{
    uv = vertex.UV.xy * materialData.TextureTilingOffset.xy + materialData.TextureTilingOffset.zw;
}

void AlphaClip(const VisibilityBufferValue visibilityBufferValue, const float2 uv)
{
    const AAAAInstanceData instanceData = PullInstanceData(visibilityBufferValue.instanceID);
    const AAAAMaterialData materialData = PullMaterialData(instanceData.MaterialIndex);

    const float4 albedo = SampleAlbedo(uv, materialData);
    clip(albedo.a - materialData.AlphaClipThreshold);
}

uint PullIndexChecked(const AAAAMeshlet meshlet, const uint indexID)
{
    if (indexID >= meshlet.TriangleCount * 3)
    {
        return -1;
    }
    return PullIndex(meshlet, indexID);
}

AAAAMeshletVertex PullVertexChecked(const AAAAMeshlet meshlet, const uint index)
{
    if (index == -1)
    {
        return (AAAAMeshletVertex)0;
    }
    return PullVertex(meshlet, index);
}

Varyings VS(const uint svInstanceID : SV_InstanceID, const uint svIndexID : SV_VertexID)
{
    InitIndirectDrawArgs(0);

    Varyings OUT;

    const AAAAMeshletRenderRequest meshletRenderRequest = PullMeshletRenderRequest(
        _MeshletRenderRequests, GetIndirectInstanceID_Base(svInstanceID));
    const uint indexID = GetIndirectVertexID_Base(svIndexID);

    const AAAAInstanceData perInstanceData = PullInstanceData(meshletRenderRequest.InstanceID);

    const AAAAMeshlet       meshlet = PullMeshletData(meshletRenderRequest.MeshletID);
    const uint              index = PullIndexChecked(meshlet, indexID);
    const AAAAMeshletVertex vertex = PullVertexChecked(meshlet, index);


    const float3 positionWS = mul(perInstanceData.ObjectToWorldMatrix, float4(vertex.Position.xyz, 1.0f)).xyz;

    OUT.positionCS = TransformWorldToHClip(positionWS);

    VisibilityBufferValue visibilityBufferValue;
    visibilityBufferValue.instanceID = meshletRenderRequest.InstanceID;
    visibilityBufferValue.meshletID = meshletRenderRequest.MeshletID;
    visibilityBufferValue.indexID = indexID;
    OUT.visibilityValue = PackVisibilityBufferValue(visibilityBufferValue);

    const AAAAMaterialData materialData = PullMaterialData(perInstanceData.MaterialIndex);

    #ifdef _ALPHATEST_ON
    WriteUV(vertex, materialData, OUT.uv0);
    #endif

    return OUT;
}

uint2 PS(const Varyings IN) : SV_TARGET
{
    const VisibilityBufferValue visibilityBufferValue = UnpackVisibilityBufferValue(IN.visibilityValue);

    #ifdef _ALPHATEST_ON
    AlphaClip(visibilityBufferValue, IN.uv0);
    #endif

    return IN.visibilityValue;
}

#endif // AAAA_VISIBILITY_BUFFER_PASS_INCLUDED