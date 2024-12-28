#ifndef AAAA_VXGI_VOXELIZE_PASS_INCLUDED
#define AAAA_VXGI_VOXELIZE_PASS_INCLUDED

#define VXGI_CONSERVATIVE_RASTERIZATION

// Sources:
// - https://github.com/turanszkij/WickedEngine/blob/97e08abfe5f1f086a845e353c832583c89f3edd3/WickedEngine/shaders/objectVS_voxelizer.hlsl
// - https://github.com/turanszkij/WickedEngine/blob/97e08abfe5f1f086a845e353c832583c89f3edd3/WickedEngine/shaders/objectGS_voxelizer.hlsl
// - https://github.com/turanszkij/WickedEngine/blob/97e08abfe5f1f086a845e353c832583c89f3edd3/WickedEngine/shaders/objectPS_voxelizer.hlsl

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Math.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VXGI.hlsl"

RWByteAddressBuffer _Result : register(u1);

#define REQUIRE_UV0_INTERPOLATOR
#include "Packages/com.deltation.aaaa-rp/Shaders/VisibilityBuffer/VisibilityBufferPass.hlsl"

struct GSInput
{
    float3 positionWS : POSITION_WS;
    float2 uv0 : TEXCOORD0;
    float3 normalWS : NORMAL_WS;
    VISIBILITY_VALUE_VARYING
};

struct GSOutput
{
    float4          positionCS : SV_POSITION;
    centroid float2 uv0 : TEXCOORD0;
    centroid float3 normalWS : NORMAL_WS;
    centroid float3 positionWS : POSITION_WS;
    nointerpolation VISIBILITY_VALUE_VARYING
    #ifdef VXGI_CONSERVATIVE_RASTERIZATION
    nointerpolation float3 aabbMin : AABB_MIN;
    nointerpolation float3 aabbMax : AABB_MAX;
    #endif
};

GSInput VoxelizeVS(const uint svInstanceID : SV_InstanceID, const uint svIndexID : SV_VertexID)
{
    float3            positionWS;
    AAAAInstanceData  instanceData;
    AAAAMeshletVertex vertex;
    Varyings          varyings = VSBase(svInstanceID, svIndexID, positionWS, instanceData, vertex);

    GSInput OUT;
    OUT.positionWS = positionWS;
    OUT.uv0 = varyings.uv0;
    OUT.normalWS = SafeNormalize(TransformObjectToWorldNormal(vertex.Normal.xyz));
    OUT.visibilityValue = varyings.visibilityValue;
    return OUT;
}

void AccumulateResult(const uint baseAddress, const uint channel, const float value)
{
    const uint fullAddress = channel * 4 + baseAddress;
    const uint packedValue = VXGI::Packing::PackChannel(value);
    _Result.InterlockedAdd(fullAddress, packedValue);
}

[maxvertexcount(3)]
void VoxelizeGS(
    triangle GSInput               IN[3],
    inout TriangleStream<GSOutput> outputStream
)
{
    VXGI::Grid grid = VXGI::Grid::LoadLevel(0);

    float3 faceNormalWS = abs(IN[0].normalWS + IN[1].normalWS + IN[2].normalWS);
    uint   maxAxis = faceNormalWS[1] > faceNormalWS[0] ? 1 : 0;
    maxAxis = faceNormalWS[2] > faceNormalWS[maxAxis] ? 2 : maxAxis;

    const float3 aabbMin = min(IN[0].positionWS.xyz, min(IN[1].positionWS.xyz, IN[2].positionWS.xyz));
    const float3 aabbMax = max(IN[0].positionWS.xyz, max(IN[1].positionWS.xyz, IN[2].positionWS.xyz));

    GSOutput OUT[3];

    for (uint i = 0; i < 3; ++i)
    {
        OUT[i].positionCS.xyz = grid.TransformWorldToGridSpace(IN[i].positionWS);

        UNITY_FLATTEN
        if (maxAxis == 0)
        {
            OUT[i].positionCS.xyz = OUT[i].positionCS.zyx;
        }
        else if (maxAxis == 1)
        {
            OUT[i].positionCS.xyz = OUT[i].positionCS.xzy;
        }
    }

    #ifdef VXGI_CONSERVATIVE_RASTERIZATION
    {
        // Expand triangle to get fake Conservative Rasterization:
        const float2 side0N = normalize(OUT[1].positionCS.xy - OUT[0].positionCS.xy);
        const float2 side1N = normalize(OUT[2].positionCS.xy - OUT[1].positionCS.xy);
        const float2 side2N = normalize(OUT[0].positionCS.xy - OUT[2].positionCS.xy);
        OUT[0].positionCS.xy += normalize(side2N - side0N);
        OUT[1].positionCS.xy += normalize(side0N - side1N);
        OUT[2].positionCS.xy += normalize(side1N - side2N);
    }
    #endif

    for (uint i = 0; i < 3; ++i)
    {
        // voxel space -> normalized screen -> NDC
        OUT[i].positionCS.xy = (OUT[i].positionCS.xy * grid.invSize) * 2 - 1;
        #ifdef UNITY_UV_STARTS_AT_TOP
        OUT[i].positionCS.xy *= -1;
        #endif
        OUT[i].positionCS.zw = 1;

        OUT[i].normalWS = IN[i].normalWS;
        OUT[i].uv0 = IN[i].uv0;
        OUT[i].positionWS = IN[i].positionWS;
        OUT[i].visibilityValue = IN[i].visibilityValue;

        #ifdef VXGI_CONSERVATIVE_RASTERIZATION
        OUT[i].aabbMin = aabbMin;
        OUT[i].aabbMax = aabbMax;
        #endif

        outputStream.Append(OUT[i]);
    }
}

struct SurfaceData
{
    float4 diffuseColor;
    float3 normalWS;
    float3 positionWS;
    float  metallic;
};

float3 ComputeFastDiffuseBRDF(const SurfaceData surfaceData, const Light light)
{
    const float  NdotL = saturate(dot(surfaceData.normalWS, light.directionWS));
    const float3 radiance = light.color;
    const float  shadowAttenuation = light.shadowAttenuation;
    const float  kD = saturate(1 - surfaceData.metallic);
    return shadowAttenuation * kD * surfaceData.diffuseColor.rgb * radiance * NdotL;
}

float3 ComputeFastDiffuseLighting(const SurfaceData surfaceData)
{
    float3 lighting = 0;

    const uint directionalLightCount = GetDirectionalLightCount();

    UNITY_UNROLLX(MAX_DIRECTIONAL_LIGHTS)
    for (uint lightIndex = 0; lightIndex < directionalLightCount; ++lightIndex)
    {
        const Light light = GetDirectionalLight(lightIndex, surfaceData.positionWS);
        lighting += ComputeFastDiffuseBRDF(surfaceData, light);
    }

    const float4                        positionCS = TransformWorldToHClip(surfaceData.positionWS);
    const float2                        positionNDC = positionCS.xy / positionCS.w;
    const float2                        positionScreenUV = NDCToScreenUV(positionNDC);
    const AAAAClusteredLightingGridCell lightGridCell = ClusteredLighting::LoadCell(surfaceData.positionWS, positionScreenUV);

    for (uint i = 0; i < lightGridCell.Count; ++i)
    {
        const uint  punctualLightIndex = ClusteredLighting::LoadLightIndex(lightGridCell, i);
        const Light light = GetPunctualLight(punctualLightIndex, surfaceData.positionWS);
        lighting += light.distanceAttenuation * ComputeFastDiffuseBRDF(surfaceData, light);
    }

    return lighting;
}

void VoxelizePS(const GSOutput IN)
{
    VXGI::Grid   grid = VXGI::Grid::LoadLevel(0);
    const float3 voxelID = grid.TransformWorldToGridSpace(IN.positionWS);

    UNITY_BRANCH
    if (any(voxelID < 0 || voxelID >= grid.size))
    {
        return;
    }

    #ifdef VXGI_CONSERVATIVE_RASTERIZATION
    {
        const float3 voxelCenterWS = grid.TransformGridToWorldSpace(floor(voxelID) + 0.5);
        const AABB   voxelAABB = AABB::CreateFromCenterSize(voxelCenterWS, grid.voxelSizeWS);
        const AABB   triangleAABB = AABB::Create(IN.aabbMin, IN.aabbMax);
        UNITY_BRANCH
        if (!AABB::Intersect(voxelAABB, triangleAABB))
        {
            return;
        }
    }
    #endif

    const VisibilityBufferValue visibilityBufferValue = UnpackVisibilityBufferValue(IN.visibilityValue);

    const AAAAInstanceData instanceData = PullInstanceData(visibilityBufferValue.instanceID);
    const AAAAMaterialData materialData = PullMaterialData(instanceData.MaterialIndex);

    const uint flatID = grid.VoxelToFlatID(voxelID);
    const uint baseAddress = VXGI::Grid::FlatIDToPackedGridAddress(flatID);

    SurfaceData surfaceData;
    surfaceData.positionWS = IN.positionWS;
    surfaceData.diffuseColor = SampleAlbedo(IN.uv0, materialData);
    surfaceData.normalWS = SafeNormalize(IN.normalWS);
    surfaceData.metallic = SampleMasks(IN.uv0, materialData).metallic;

    UNITY_BRANCH
    if (surfaceData.diffuseColor.a < materialData.AlphaClipThreshold)
    {
        discard;
        return;
    }

    AccumulateResult(baseAddress, AAAAVXGIPACKEDGRIDCHANNELS_BASE_COLOR_R, surfaceData.diffuseColor.r);
    AccumulateResult(baseAddress, AAAAVXGIPACKEDGRIDCHANNELS_BASE_COLOR_G, surfaceData.diffuseColor.g);
    AccumulateResult(baseAddress, AAAAVXGIPACKEDGRIDCHANNELS_BASE_COLOR_B, surfaceData.diffuseColor.b);
    AccumulateResult(baseAddress, AAAAVXGIPACKEDGRIDCHANNELS_BASE_COLOR_A, surfaceData.diffuseColor.a);

    const float3 emission = surfaceData.diffuseColor.rgb * materialData.Emission.rgb;
    AccumulateResult(baseAddress, AAAAVXGIPACKEDGRIDCHANNELS_EMISSIVE_R, emission.r);
    AccumulateResult(baseAddress, AAAAVXGIPACKEDGRIDCHANNELS_EMISSIVE_G, emission.g);
    AccumulateResult(baseAddress, AAAAVXGIPACKEDGRIDCHANNELS_EMISSIVE_B, emission.b);

    const float3 directLighting = ComputeFastDiffuseLighting(surfaceData);
    AccumulateResult(baseAddress, AAAAVXGIPACKEDGRIDCHANNELS_DIRECT_LIGHT_R, directLighting.r);
    AccumulateResult(baseAddress, AAAAVXGIPACKEDGRIDCHANNELS_DIRECT_LIGHT_G, directLighting.g);
    AccumulateResult(baseAddress, AAAAVXGIPACKEDGRIDCHANNELS_DIRECT_LIGHT_B, directLighting.b);

    const float2 packedNormal = VXGI::Packing::PackNormal(surfaceData.normalWS);
    AccumulateResult(baseAddress, AAAAVXGIPACKEDGRIDCHANNELS_PACKED_NORMAL_R, packedNormal.r);
    AccumulateResult(baseAddress, AAAAVXGIPACKEDGRIDCHANNELS_PACKED_NORMAL_G, packedNormal.g);

    AccumulateResult(baseAddress, AAAAVXGIPACKEDGRIDCHANNELS_FRAGMENT_COUNT, 1);
}

#endif // AAAA_VXGI_VOXELIZE_PASS_INCLUDED