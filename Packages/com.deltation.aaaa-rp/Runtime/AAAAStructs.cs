using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace DELTation.AAAARP
{
    [GenerateHLSL(PackingRules.Exact, needAccessors = false)]
    public struct AAAAInstanceData
    {
        public float4x4 ObjectToWorldMatrix;
        public float4x4 WorldToObjectMatrix;

        public uint MeshletStartOffset;
        public uint MeshletCount;

        public uint MaterialIndex;
        public uint Padding0;
    }

    [GenerateHLSL(PackingRules.Exact, needAccessors = false)]
    public struct AAAAMaterialData
    {
        public uint AlbedoIndex;
        public uint Padding0;
        public uint Padding1;
        public uint Padding2;
    }

    [GenerateHLSL]
    public static class AAAAMeshletConfiguration
    {
        public const uint MaxMeshletVertices = 128;
        public const uint MaxMeshletTriangles = 128;
        public const uint MaxMeshletIndices = MaxMeshletTriangles * 3;
        public const float MeshletConeWeight = 0.5f;
    }

    [GenerateHLSL(PackingRules.Exact, needAccessors = false)]
    [Serializable]
    public struct AAAAMeshlet
    {
        public uint VertexOffset;
        public uint TriangleOffset;
        public uint VertexCount;
        public uint TriangleCount;

        public float4 BoundingSphere;
    }

    [GenerateHLSL(PackingRules.Exact, needAccessors = false)]
    [StructLayout(LayoutKind.Sequential)]
    [Serializable]
    public struct AAAAMeshletVertex
    {
        public float4 Position;
        public float4 Normal;
        public float4 Tangent;
        public float4 UV;
    }

    [GenerateHLSL(PackingRules.Exact, needAccessors = false)]
    [StructLayout(LayoutKind.Sequential)]
    public struct AAAAMeshletRenderRequest
    {
        public uint InstanceID;
        public uint RelativeMeshletID;
    }
}