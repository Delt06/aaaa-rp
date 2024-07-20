using System;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace DELTation.AAAARP
{
    [GenerateHLSL(PackingRules.Exact, needAccessors = false)]
    [StructLayout(LayoutKind.Sequential)]
    public struct AAAAInstanceData
    {
        public float4x4 ObjectToWorldMatrix;
        public float4x4 WorldToObjectMatrix;
        public float4 AABBMin;
        public float4 AABBMax;

        public uint MeshLODStartIndex;
        public uint MaterialIndex;
        public uint Padding0;
        public uint Padding1;
    }

    [GenerateHLSL(PackingRules.Exact, needAccessors = false)]
    [StructLayout(LayoutKind.Sequential)]
    public struct AAAAMaterialData
    {
        public float4 AlbedoColor;
        public uint AlbedoIndex;
        public uint Padding0;
        public uint Padding1;
        public uint Padding2;

        public const uint NoTextureIndex = uint.MaxValue;
    }

    [GenerateHLSL]
    public static class AAAAMeshletConfiguration
    {
        [UsedImplicitly]
        public const uint MaxMeshletVertices = 32;
        [UsedImplicitly]
        public const uint MaxMeshletTriangles = 128;
        [UsedImplicitly]
        public const uint MaxMeshletIndices = MaxMeshletTriangles * 3;
        [UsedImplicitly]
        public const float MeshletConeWeight = 0.25f;
        [UsedImplicitly]
        public const uint LodBits = 3;
        [UsedImplicitly]
        public const uint LodCount = 1u << (int) LodBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    [GenerateHLSL(PackingRules.Exact, needAccessors = false)]
    [Serializable]
    public struct AAAAMeshLOD
    {
        public uint MeshletStartOffset;
        public uint MeshletCount;
        public uint Padding0;
        public uint Padding1;
    }

    [GenerateHLSL(PackingRules.Exact, needAccessors = false)]
    [StructLayout(LayoutKind.Sequential)]
    [Serializable]
    public struct AAAAMeshlet
    {
        public uint VertexOffset;
        public uint TriangleOffset;
        public uint VertexCount;
        public uint TriangleCount;

        public float4 BoundingSphere;
        public float4 ConeApexCutoff;
        public float4 ConeAxis;
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
    public struct AAAAMeshletRenderRequestPacked
    {
        public uint InstanceID_LOD;
        public uint MeshletID;
    }

    [GenerateHLSL(PackingRules.Exact, needAccessors = false)]
    [StructLayout(LayoutKind.Sequential)]
    public struct IndirectDispatchArgs
    {
        public uint ThreadGroupsX;
        public uint ThreadGroupsY;
        public uint ThreadGroupsZ;
        public uint Padding0;
    }
}