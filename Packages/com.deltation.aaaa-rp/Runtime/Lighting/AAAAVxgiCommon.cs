﻿using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Lighting
{
    /// <summary>
    ///     Sources:
    ///     - https://wickedengine.net/2017/08/voxel-based-global-illumination/
    /// </summary>
    internal static class AAAAVxgiCommon
    {
        public const string ResourceNamePrefix = "VXGI_";

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public static class GlobalShaderIDs
        {
            public static readonly int _VXGILevelCount = Shader.PropertyToID(nameof(_VXGILevelCount));
            public static readonly int _VXGIRadiance = Shader.PropertyToID(nameof(_VXGIRadiance));
        }
    }

    [GenerateHLSL]
    internal enum AAAAVxgiPackedGridChannels
    {
        BaseColorR = 0,
        BaseColorG,
        BaseColorB,
        BaseColorA,
        EmissiveR,
        EmissiveG,
        EmissiveB,
        DirectLightR,
        DirectLightG,
        DirectLightB,
        PackedNormalR,
        PackedNormalG,
        FragmentCount,
        TotalCount,
    }
}