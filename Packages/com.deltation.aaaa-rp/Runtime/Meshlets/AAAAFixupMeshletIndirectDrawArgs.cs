using UnityEngine.Rendering;

namespace DELTation.AAAARP.Meshlets
{
    [GenerateHLSL]
    public static class AAAAFixupMeshletIndirectDrawArgs
    {
        public const int ThreadGroupSize = 1;
        public const int KernelIndex = 0;
    }
}