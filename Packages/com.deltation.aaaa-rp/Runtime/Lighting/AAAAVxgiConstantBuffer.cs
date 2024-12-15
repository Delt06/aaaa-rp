using System.Diagnostics.CodeAnalysis;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Lighting
{
    [GenerateHLSL(PackingRules.Exact, needAccessors = false, generateCBuffer = true)]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public struct AAAAVxgiConstantBuffer
    {
        public float4 _VxgiGridBoundsMin;
        public float4 _VxgiGridBoundsMax;
        public float4 _VxgiGridResolution;
    }
}