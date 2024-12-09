using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Lighting
{
    [GenerateHLSL(PackingRules.Exact, needAccessors = false, generateCBuffer = true)]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public unsafe struct AAAALpvConstantBuffer
    {
        [HLSLArray(AAAALightingConstantBuffer.MaxDirectionalLights, typeof(Vector4))]
        public fixed float DirectionalLightRsmBindlessIndices[AAAALightingConstantBuffer.MaxDirectionalLights * 4];
    }
}