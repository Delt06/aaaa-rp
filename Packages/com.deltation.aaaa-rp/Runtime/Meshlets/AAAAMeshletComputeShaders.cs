﻿using JetBrains.Annotations;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Meshlets
{
    [GenerateHLSL]
    public static class AAAAMeshletComputeShaders
    {
        [UsedImplicitly]
        public const uint MaxMeshLODNodesPerInstance = 128 * 128;

        [UsedImplicitly]
        public const uint GPUInstanceCullingThreadGroupSize = 32;
        [UsedImplicitly]
        public const uint MeshletListBuildThreadGroupSize = 32;

        [UsedImplicitly]
        public const uint GPUMeshletCullingThreadGroupSize = 32;
    }

    [GenerateHLSL]
    public struct AAAAMeshletListBuildJob
    {
        [UsedImplicitly]
        public const uint MaxLODNodesPerThreadGroup = AAAAMeshletComputeShaders.MeshletListBuildThreadGroupSize * 4;
        
        public uint InstanceID;
        public uint MeshLODNodeOffset;
        public uint MeshLODNodeCount;
        public uint Padding0;
    }
}