//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef AAAASTRUCTS_CS_HLSL
#define AAAASTRUCTS_CS_HLSL
//
// DELTation.AAAARP.AAAAMaterialData:  static fields
//
#define NO_TEXTURE_INDEX (4294967295)

//
// DELTation.AAAARP.AAAAMeshletConfiguration:  static fields
//
#define MAX_MESHLET_VERTICES (255)
#define MAX_MESHLET_TRIANGLES (256)
#define MAX_MESHLET_INDICES (768)
#define MESHLET_CONE_WEIGHT (0)

// Generated from DELTation.AAAARP.AAAAInstanceData
// PackingRules = Exact
struct AAAAInstanceData
{
    float4x4 ObjectToWorldMatrix;
    float4x4 WorldToObjectMatrix;
    uint MeshletStartOffset;
    uint MeshletCount;
    uint MaterialIndex;
    uint Padding0;
};

// Generated from DELTation.AAAARP.AAAAMaterialData
// PackingRules = Exact
struct AAAAMaterialData
{
    float4 AlbedoColor;
    uint AlbedoIndex;
    uint Padding0;
    uint Padding1;
    uint Padding2;
};

// Generated from DELTation.AAAARP.AAAAMeshlet
// PackingRules = Exact
struct AAAAMeshlet
{
    uint VertexOffset;
    uint TriangleOffset;
    uint VertexCount;
    uint TriangleCount;
    float4 BoundingSphere;
    float4 ConeApexCutoff;
    float4 ConeAxis;
};

// Generated from DELTation.AAAARP.AAAAMeshletRenderRequest
// PackingRules = Exact
struct AAAAMeshletRenderRequest
{
    uint InstanceID;
    uint RelativeMeshletID;
};

// Generated from DELTation.AAAARP.AAAAMeshletVertex
// PackingRules = Exact
struct AAAAMeshletVertex
{
    float4 Position;
    float4 Normal;
    float4 Tangent;
    float4 UV;
};


#endif
