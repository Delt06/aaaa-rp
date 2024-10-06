using Unity.Mathematics;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Shaders.PostProcessing.FSR
{
    [GenerateHLSL(PackingRules.Exact, needAccessors = false, generateCBuffer = true)]
    public struct AAAAFSRConstantBuffer
    {
        public uint4 Const0;
        public uint4 Const1;
        public uint4 Const2;
        public uint4 Const3;
        public uint4 Sample;

        public const int ThreadGroupSizeX = 16;
        public const int ThreadGroupSizeY = 16;
    }
}