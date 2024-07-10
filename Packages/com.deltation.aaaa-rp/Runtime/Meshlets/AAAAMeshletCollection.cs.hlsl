//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef AAAAMESHLETCOLLECTION_CS_HLSL
#define AAAAMESHLETCOLLECTION_CS_HLSL
//
// DELTation.AAAARP.Meshlets.AAAAMeshletCollection:  static fields
//
#define MAX_MESHLET_VERTICES (128)
#define MAX_MESHLET_TRIANGLES (128)
#define MAX_MESHLET_INDICES (384)
#define MESHLET_CONE_WEIGHT (0.5)

// Generated from DELTation.AAAARP.Meshlets.AAAAMeshlet
// PackingRules = Exact
struct AAAAMeshlet
{
    uint   VertexOffset;
    uint   TriangleOffset;
    uint   VertexCount;
    uint   TriangleCount;
    float4 BoundingSphere;
};

// Generated from DELTation.AAAARP.Meshlets.AAAAMeshletVertex
// PackingRules = Exact
struct AAAAMeshletVertex
{
    float4 Position;
    float4 Normal;
    float4 Tangent;
    float4 UV;
};

// Generated from DELTation.AAAARP.Meshlets.AAAAPerInstanceData
// PackingRules = Exact
struct AAAAPerInstanceData
{
    float4x4 ObjectToWorldMatrix;
    float4x4 WorldToObjectMatrix;
};


#endif