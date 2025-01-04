using System.Diagnostics.CodeAnalysis;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Lighting
{
    [GenerateHLSL(PackingRules.Exact, needAccessors = false, generateCBuffer = true)]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public unsafe struct AAAAVxgiConstantBuffer
    {
        [HLSLArray(AAAAVxgiCommon.MaxMipLevels, typeof(Vector4))]
        public fixed float _VxgiGridBoundsMin[AAAAVxgiCommon.MaxMipLevels * 4];
        public float4 _VxgiGridResolution;
        public uint _VxgiLevelCount;
        public float _VxgiDiffuseOpacityFactor;
        public float _VxgiSpecularOpacityFactor;
    }
}