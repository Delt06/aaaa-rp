using System;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Debugging
{
    [GenerateHLSL(PackingRules.Exact, false)]
    [Serializable]
    public struct AAAAGPUCullingDebugData
    {
        public const uint GPUCullingDebugBufferDimension = 16;

        public uint FrustumCulledInstances;
        public uint FrustumCulledMeshlets;
        public uint OcclusionCulledInstances;
        public uint OcclusionCulledMeshlets;
        public uint ConeCulledMeshlets;
    }

    [GenerateHLSL]
    public enum AAAAGPUCullingDebugType
    {
        Frustum,
        Occlusion,
        Cone,
    }

    [GenerateHLSL]
    public enum AAAAGPUCullingDebugGranularity
    {
        Instance,
        Meshlet,
    }
}