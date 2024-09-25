using UnityEngine.Rendering;

namespace DELTation.AAAARP.Debugging
{
    [GenerateHLSL(PackingRules.Exact, false)]
    public struct AAAAGPUCullingDebugData
    {
        public const uint GPUCullingDebugBufferDimension = 16;

        public uint OcclusionCulledInstances;
        public uint OcclusionCulledMeshlets;
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