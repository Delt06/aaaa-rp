using Unity.Mathematics;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Passes.ClusteredLighting
{
    [GenerateHLSL(PackingRules.Exact, false)]
    public static class AAAAClusteredLightingComputeShaders
    {
        public const int BuildClusterGridThreadGroupSize = 32;
        public const int ClusterCullingThreadGroupSize = 32;
    }

    [GenerateHLSL(PackingRules.Exact, false, generateCBuffer: true)]
    public struct AAAAClusteredLightingConstantBuffer
    {
        public const int ClustersX = 16;
        public const int ClustersY = 9;
        public const int ClustersZ = 24;

        public const int TotalClusters = ClustersX * ClustersY * ClustersZ;
    }

    [GenerateHLSL(PackingRules.Exact, false)]
    public struct AAAAClusterBounds
    {
        public float4 Min;
        public float4 Max;
    }
}