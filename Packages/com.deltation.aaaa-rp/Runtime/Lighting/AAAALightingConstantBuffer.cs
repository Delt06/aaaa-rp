using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Lighting
{
    [GenerateHLSL(PackingRules.Exact, needAccessors = false, generateCBuffer = true)]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public unsafe struct AAAALightingConstantBuffer
    {
        public const int MaxDirectionalLights = 4;

        [HLSLArray(MaxDirectionalLights, typeof(Vector4))]
        public fixed float DirectionalLightColors[MaxDirectionalLights * 4];

        [HLSLArray(MaxDirectionalLights, typeof(Vector4))]
        public fixed float DirectionalLightDirections_SoftShadow[MaxDirectionalLights * 4];
        
        [HLSLArray(MaxDirectionalLights, typeof(Vector4))]
        public fixed float DirectionalLightShadowSliceRanges_ShadowFadeParams[MaxDirectionalLights * 4];

        public uint DirectionalLightCount;
        public uint PunctualLightCount;
    }
}