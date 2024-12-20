﻿using System.Diagnostics.CodeAnalysis;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Lighting
{
    [GenerateHLSL(PackingRules.Exact, needAccessors = false)]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public struct AAAAPunctualLightData
    {
        public float4 Color_Radius;
        public float4 PositionWS;
        public float4 SpotDirection_Angle;
        public float4 Attenuations;
        public float4 ShadowSliceIndex_ShadowFadeParams;
        public float4 ShadowParams;
    }
}