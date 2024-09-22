using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace DELTation.AAAARP.Renderers
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal static class RendererContainerShaderIDs
    {
        public static readonly int _InstanceData = Shader.PropertyToID(nameof(_InstanceData));
        public static readonly int _InstanceCount = Shader.PropertyToID(nameof(_InstanceCount));
    }
}