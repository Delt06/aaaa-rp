using System;
using Unity.Mathematics;
using UnityEngine;

namespace DELTation.AAAARP.Meshlets
{
    public class MeshletCollection : ScriptableObject
    {
        public Meshlet[] Meshlets = Array.Empty<Meshlet>();
        public float[] VertexBuffer = Array.Empty<float>();
        public ushort[] IndexBuffer = Array.Empty<ushort>();
    }
    
    [Serializable]
    public struct Meshlet
    {
        public int VertexOffset;
        public int TriangleOffset;
        public int VertexCount;
        public int TriangleCount;
        
        public float4 BoundingSphere;
    }
}