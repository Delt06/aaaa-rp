using System;
using System.Runtime.InteropServices;
using DELTation.AAAARP.MeshOptimizer.Runtime;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Meshlets
{
    [GenerateHLSL]
    public class AAAAMeshletCollection : ScriptableObject
    {
        public const uint MaxMeshletVertices = 128;
        public const uint MaxMeshletTriangles = 128;
        public const uint MaxMeshletIndices = MaxMeshletTriangles * 3;
        public const float MeshletConeWeight = 0.5f;
        
        public static readonly AAAAMeshOptimizer.MeshletGenerationParams MeshletGenerationParams = new()
        {
            MaxVertices = MaxMeshletVertices,
            MaxTriangles = MaxMeshletTriangles,
            ConeWeight = MeshletConeWeight,
        };
        
        public AAAAMeshlet[] Meshlets = Array.Empty<AAAAMeshlet>();
        public AAAAMeshletVertex[] VertexBuffer = Array.Empty<AAAAMeshletVertex>();
        public byte[] IndexBuffer = Array.Empty<byte>();
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
        public float2 UV;
    }
}