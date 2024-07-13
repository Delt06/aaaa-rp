using UnityEngine.Rendering;

namespace DELTation.AAAARP.Meshlets
{
    [GenerateHLSL]
    public static class AAAAGPUMeshletCulling
    {
        public const int ThreadGroupSize = 32;
        public const int KernelIndex = 0;
    }
}