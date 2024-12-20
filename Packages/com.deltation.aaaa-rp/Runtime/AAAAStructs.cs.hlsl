//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef AAAASTRUCTS_CS_HLSL
#define AAAASTRUCTS_CS_HLSL
//
// DELTation.AAAARP.AAAAGeometryFlags:  static fields
//
#define AAAAGEOMETRYFLAGS_NONE (0)
#define AAAAGEOMETRYFLAGS_SPECULAR_AA (1)

//
// DELTation.AAAARP.AAAAInstanceFlags:  static fields
//
#define AAAAINSTANCEFLAGS_NONE (0)
#define AAAAINSTANCEFLAGS_DISABLED (1)
#define AAAAINSTANCEFLAGS_FLIP_WINDING_ORDER (2)

//
// DELTation.AAAARP.AAAAInstancePassMask:  static fields
//
#define AAAAINSTANCEPASSMASK_NONE (0)
#define AAAAINSTANCEPASSMASK_MAIN (1)
#define AAAAINSTANCEPASSMASK_SHADOWS (2)

//
// DELTation.AAAARP.AAAAMaterialFlags:  static fields
//
#define AAAAMATERIALFLAGS_NONE (0)
#define AAAAMATERIALFLAGS_UNLIT (1)

//
// DELTation.AAAARP.AAAARendererListID:  static fields
//
#define AAAARENDERERLISTID_DEFAULT (0)
#define AAAARENDERERLISTID_CULL_FRONT (1)
#define AAAARENDERERLISTID_CULL_OFF (2)
#define AAAARENDERERLISTID_ALPHA_TEST (4)
#define AAAARENDERERLISTID_COUNT (8)

//
// DELTation.AAAARP.AAAAMaterialData:  static fields
//
#define NO_TEXTURE_INDEX (4294967295)

//
// DELTation.AAAARP.AAAAMeshletConfiguration:  static fields
//
#define MAX_MESHLET_VERTICES (128)
#define MAX_MESHLET_TRIANGLES (128)
#define MAX_MESHLET_INDICES (384)
#define MESHLET_CONE_WEIGHT (0.25)

// Generated from DELTation.AAAARP.AAAAInstanceData
// PackingRules = Exact
struct AAAAInstanceData
{
    float4x4 ObjectToWorldMatrix;
    float4x4 WorldToObjectMatrix;
    float4 AABBMin;
    float4 AABBMax;
    uint TopMeshLODStartIndex;
    uint TotalMeshLODCount;
    uint MaterialIndex;
    uint MeshLODLevelCount;
    float LODErrorScale;
    int PassMask;
    int Flags;
    uint Padding0;
};

// Generated from DELTation.AAAARP.AAAAMaterialData
// PackingRules = Exact
struct AAAAMaterialData
{
    float4 AlbedoColor;
    float4 TextureTilingOffset;
    float4 Emission;
    uint AlbedoIndex;
    uint NormalsIndex;
    float NormalsStrength;
    uint MasksIndex;
    float Roughness;
    float Metallic;
    float SpecularAAScreenSpaceVariance;
    float SpecularAAThreshold;
    int GeometryFlags;
    int MaterialFlags;
    int RendererListID;
    float AlphaClipThreshold;
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

// Generated from DELTation.AAAARP.AAAAMeshletRenderRequestPacked
// PackingRules = Exact
struct AAAAMeshletRenderRequestPacked
{
    uint InstanceID_LOD;
    uint MeshletID;
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

// Generated from DELTation.AAAARP.AAAAMeshLODNode
// PackingRules = Exact
struct AAAAMeshLODNode
{
    float4 Bounds;
    float4 ParentBounds;
    float ParentError;
    float Error;
    uint MeshletStartIndex;
    uint MeshletCount;
    uint LevelIndex;
    uint Padding0;
    uint Padding1;
    uint Padding2;
};

// Generated from DELTation.AAAARP.IndirectDispatchArgs
// PackingRules = Exact
struct IndirectDispatchArgs
{
    uint ThreadGroupsX;
    uint ThreadGroupsY;
    uint ThreadGroupsZ;
};


#endif
