using System.Diagnostics.CodeAnalysis;
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
        public const int MaxMipLevels = 16;
        public const string ResourceNamePrefix = "VXGI_";

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public static class GlobalShaderIDs
        {
            public static readonly int _VXGIRadiance = Shader.PropertyToID(nameof(_VXGIRadiance));
            public static readonly int _VXGIIndirectDiffuseTexture = Shader.PropertyToID(nameof(_VXGIIndirectDiffuseTexture));
            public static readonly int _VXGIIndirectSpecularTexture = Shader.PropertyToID(nameof(_VXGIIndirectSpecularTexture));
        }
    }

    [GenerateHLSL]
    internal enum AAAAVxgiPackedGridChannels
    {
        RadianceR = 0,
        RadianceG,
        RadianceB,
        Alpha,
        PackedNormalR,
        PackedNormalG,
        FragmentCount,
        TotalCount,
    }
}