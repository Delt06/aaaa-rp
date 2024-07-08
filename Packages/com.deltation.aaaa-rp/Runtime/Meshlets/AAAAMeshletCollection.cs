using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Meshlets
{
    public class AAAAMeshletCollection : ScriptableObject
    {
        public AAAAMeshlet[] Meshlets = Array.Empty<AAAAMeshlet>();
        public AAAAMeshletVertex[] VertexBuffer = Array.Empty<AAAAMeshletVertex>();
        public ushort[] IndexBuffer = Array.Empty<ushort>();
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
    [Serializable]
    public struct AAAAMeshletVertex
    {
        public float3 Position;
        public float3 Normal;
        public float4 Tangent;
        public float2 UV;
    }
}