using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace DELTation.AAAARP.Renderers
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal static class RendererContainerShaderIDs
    {
        public static readonly int _InstanceData = Shader.PropertyToID(nameof(_InstanceData));

        public static readonly int _OcclusionCulling_InstanceVisibilityMask = Shader.PropertyToID(nameof(_OcclusionCulling_InstanceVisibilityMask));
        public static readonly int _OcclusionCulling_PrevInstanceVisibilityMask = Shader.PropertyToID(nameof(_OcclusionCulling_PrevInstanceVisibilityMask));
    }
}