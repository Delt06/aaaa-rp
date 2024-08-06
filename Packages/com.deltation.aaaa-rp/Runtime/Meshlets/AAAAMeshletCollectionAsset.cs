using System;
using DELTation.AAAARP.MeshOptimizer.Runtime;
using UnityEngine;

namespace DELTation.AAAARP.Meshlets
{
    public class AAAAMeshletCollectionAsset : ScriptableObject
    {
        public static readonly AAAAMeshOptimizer.MeshletGenerationParams MeshletGenerationParams = new()
        {
            MaxVertices = AAAAMeshletConfiguration.MaxMeshletVertices,
            MaxTriangles = AAAAMeshletConfiguration.MaxMeshletTriangles,
            ConeWeight = AAAAMeshletConfiguration.MeshletConeWeight,
        };

        public Bounds Bounds;
        public int MeshLODLevelCount;
        public int[] MeshLODLevelNodeCounts = Array.Empty<int>();
        public AAAAMeshLODNode[] MeshLODNodes = Array.Empty<AAAAMeshLODNode>();
        public AAAAMeshlet[] Meshlets = Array.Empty<AAAAMeshlet>();
        public AAAAMeshletVertex[] VertexBuffer = Array.Empty<AAAAMeshletVertex>();
        public byte[] IndexBuffer = Array.Empty<byte>();
    }
}