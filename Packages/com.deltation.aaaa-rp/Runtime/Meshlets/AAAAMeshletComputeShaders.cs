using JetBrains.Annotations;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Meshlets
{
    [GenerateHLSL]
    public static class AAAAMeshletComputeShaders
    {
        [UsedImplicitly]
        public const uint MaxMeshLODNodesPerInstance = 32 * 128;

        [UsedImplicitly]
        public const uint MeshletListBuildThreadGroupSize = 32;
        [UsedImplicitly]
        public const uint FixupGPUMeshletCullingIndirectDispatchArgsThreadGroupSize = 1;

        [UsedImplicitly]
        public const uint GPUMeshletCullingThreadGroupSize = 32;
        [UsedImplicitly]
        public const uint FixupMeshletIndirectDrawArgsThreadGroupSize = 1;
    }
}