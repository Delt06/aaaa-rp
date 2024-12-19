#ifndef AAAA_VXGI_VOXELIZE_PASS_INCLUDED
#define AAAA_VXGI_VOXELIZE_PASS_INCLUDED

// Sources:
// - https://github.com/turanszkij/WickedEngine/blob/97e08abfe5f1f086a845e353c832583c89f3edd3/WickedEngine/shaders/objectVS_voxelizer.hlsl
// - https://github.com/turanszkij/WickedEngine/blob/97e08abfe5f1f086a845e353c832583c89f3edd3/WickedEngine/shaders/objectGS_voxelizer.hlsl
// - https://github.com/turanszkij/WickedEngine/blob/97e08abfe5f1f086a845e353c832583c89f3edd3/WickedEngine/shaders/objectPS_voxelizer.hlsl

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
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
    OUT.normalWS = TransformObjectToWorldNormal(vertex.Normal.xyz);
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
    VXGI::Grid grid = VXGI::Grid::Load();

    float3 faceNormalWS = abs(IN[0].normalWS + IN[1].normalWS + IN[2].normalWS);
    uint   maxAxis = faceNormalWS[1] > faceNormalWS[0] ? 1 : 0;
    maxAxis = faceNormalWS[1] > faceNormalWS[maxAxis] ? 2 : maxAxis;

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
        outputStream.Append(OUT[i]);
    }
}

void VoxelizePS(const GSOutput IN)
{
    const VisibilityBufferValue visibilityBufferValue = UnpackVisibilityBufferValue(IN.visibilityValue);

    const AAAAInstanceData instanceData = PullInstanceData(visibilityBufferValue.instanceID);
    const AAAAMaterialData materialData = PullMaterialData(instanceData.MaterialIndex);

    const float4 albedo = SampleAlbedo(IN.uv0, materialData);

    VXGI::Grid grid = VXGI::Grid::Load();

    const float3 voxelID = grid.TransformWorldToGridSpace(IN.positionWS);
    const uint   flatID = grid.VoxelToFlatID(voxelID);
    const uint   baseAddress = VXGI::Grid::FlatIDToPackedGridAddress(flatID);
    AccumulateResult(baseAddress, AAAAVXGIPACKEDGRIDCHANNELS_BASE_COLOR_R, albedo.r);
    AccumulateResult(baseAddress, AAAAVXGIPACKEDGRIDCHANNELS_BASE_COLOR_G, albedo.g);
    AccumulateResult(baseAddress, AAAAVXGIPACKEDGRIDCHANNELS_BASE_COLOR_B, albedo.b);
    AccumulateResult(baseAddress, AAAAVXGIPACKEDGRIDCHANNELS_BASE_COLOR_A, albedo.a);
    AccumulateResult(baseAddress, AAAAVXGIPACKEDGRIDCHANNELS_FRAGMENT_COUNT, 1);
}

#endif // AAAA_VXGI_VOXELIZE_PASS_INCLUDED