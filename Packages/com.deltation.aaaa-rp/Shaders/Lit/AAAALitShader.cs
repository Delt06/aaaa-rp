using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace DELTation.AAAARP.Shaders.Lit
{
    public static class AAAALitShader
    {
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public static class ShaderIDs
        {
            public static readonly int _BaseColor = Shader.PropertyToID(nameof(_BaseColor));
            public static readonly int _BaseMap = Shader.PropertyToID(nameof(_BaseMap));
            public static readonly int _AlphaClip = Shader.PropertyToID(nameof(_AlphaClip));
            public static readonly int _AlphaClipThreshold = Shader.PropertyToID(nameof(_AlphaClipThreshold));
            public static readonly int _EmissionColor = Shader.PropertyToID(nameof(_EmissionColor));
            public static readonly int _BumpMap = Shader.PropertyToID(nameof(_BumpMap));
            public static readonly int _BumpMapScale = Shader.PropertyToID(nameof(_BumpMapScale));
            public static readonly int _CullMode = Shader.PropertyToID(nameof(_CullMode));
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public static class Keywords
        {
            public static readonly string _ALPHATEST_ON = nameof(_ALPHATEST_ON);
        }
    }
}