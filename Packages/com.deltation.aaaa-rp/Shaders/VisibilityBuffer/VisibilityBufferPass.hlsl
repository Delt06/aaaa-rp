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

#if !defined(REQUIRE_UV0_INTERPOLATOR) && defined(_ALPHATEST_ON)
#define REQUIRE_UV0_INTERPOLATOR
#endif

#if defined(_ALPHATEST_ON) || defined(LPV_REFLECTIVE_SHADOW_MAPS)
#undef EXTRA_VARYINGS
#define EXTRA_VARYINGS 
#endif

struct Varyings
{
    float4 positionCS : SV_POSITION;
    VISIBILITY_VALUE_VARYING
    #ifdef REQUIRE_UV0_INTERPOLATOR
    float2 uv0 : TEXCOORD0;
    #endif
};

void WriteUV(const AAAAMeshletVertex vertex, const AAAAMaterialData materialData, out float2 uv)
{
    uv = vertex.UV.xy * materialData.TextureTilingOffset.xy + materialData.TextureTilingOffset.zw;
}

void AlphaClip(const AAAAMaterialData materialData, const float4 albedo)
{
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

Varyings VSBase(const uint svInstanceID, const uint svIndexID, out float3 positionWS, out AAAAInstanceData instanceData, out AAAAMeshletVertex vertex)
{
    InitIndirectDrawArgs(0);

    Varyings OUT;

    const AAAAMeshletRenderRequest meshletRenderRequest = PullMeshletRenderRequest(
        _MeshletRenderRequests, 0, GetIndirectInstanceID_Base(svInstanceID));
    const uint indexID = GetIndirectVertexID_Base(svIndexID);

    instanceData = PullInstanceData(meshletRenderRequest.InstanceID);

    const AAAAMeshlet meshlet = PullMeshletData(meshletRenderRequest.MeshletID);
    const uint        index = PullIndexChecked(meshlet, indexID);
    vertex = PullVertexChecked(meshlet, index);

    positionWS = mul(instanceData.ObjectToWorldMatrix, float4(vertex.Position.xyz, 1.0f)).xyz;

    OUT.positionCS = TransformWorldToHClip(positionWS);

    VisibilityBufferValue visibilityBufferValue;
    visibilityBufferValue.instanceID = meshletRenderRequest.InstanceID;
    visibilityBufferValue.meshletID = meshletRenderRequest.MeshletID;
    visibilityBufferValue.indexID = indexID;
    OUT.visibilityValue = PackVisibilityBufferValue(visibilityBufferValue);

    const AAAAMaterialData materialData = PullMaterialData(instanceData.MaterialIndex);

    #ifdef REQUIRE_UV0_INTERPOLATOR
    WriteUV(vertex, materialData, OUT.uv0);
    #endif

    return OUT;
}

Varyings VS(const uint svInstanceID : SV_InstanceID, const uint svIndexID : SV_VertexID)
{
    float3            positionWS;
    AAAAInstanceData  instanceData;
    AAAAMeshletVertex vertex;
    return VSBase(svInstanceID, svIndexID, positionWS, instanceData, vertex);
}

uint2 PS(const Varyings IN) : SV_TARGET
{
    const VisibilityBufferValue visibilityBufferValue = UnpackVisibilityBufferValue(IN.visibilityValue);

    #ifdef _ALPHATEST_ON
    const AAAAInstanceData      instanceData = PullInstanceData(visibilityBufferValue.instanceID);
    const AAAAMaterialData      materialData = PullMaterialData(instanceData.MaterialIndex);
    const float4 albedo = SampleAlbedo(IN.uv0, materialData);
    AlphaClip(materialData, albedo);
    #endif

    return IN.visibilityValue;
}

#endif // AAAA_VISIBILITY_BUFFER_PASS_INCLUDED