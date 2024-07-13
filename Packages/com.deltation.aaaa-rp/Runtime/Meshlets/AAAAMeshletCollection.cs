using System;
using DELTation.AAAARP.MeshOptimizer.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Meshlets
{
    [GenerateHLSL]
    public class AAAAMeshletCollection : ScriptableObject
    {
        public static readonly AAAAMeshOptimizer.MeshletGenerationParams MeshletGenerationParams = new()
        {
            MaxVertices = AAAAMeshletConfiguration.MaxMeshletVertices,
            MaxTriangles = AAAAMeshletConfiguration.MaxMeshletTriangles,
            ConeWeight = AAAAMeshletConfiguration.MeshletConeWeight,
        };

        [HideInInspector]
        public AAAAMeshlet[] Meshlets = Array.Empty<AAAAMeshlet>();
        [HideInInspector]
        public AAAAMeshletVertex[] VertexBuffer = Array.Empty<AAAAMeshletVertex>();
        [HideInInspector]
        public byte[] IndexBuffer = Array.Empty<byte>();
    }
}