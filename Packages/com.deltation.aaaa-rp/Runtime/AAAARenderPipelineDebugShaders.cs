using System;
using DELTation.AAAARP.Data;
using UnityEngine;
using UnityEngine.Categorization;
using UnityEngine.Rendering;

namespace DELTation.AAAARP
{
    [Serializable]
    [SupportedOnRenderPipeline(typeof(AAAARenderPipelineAsset))]
    [CategoryInfo(Name = "R: Debug Shaders", Order = 1000)]
    public class AAAARenderPipelineDebugShaders : IRenderPipelineResources
    {
        [SerializeField] [HideInInspector] private int _version;

        [SerializeField]
        [ResourcePath("Shaders/Debugging/VisibilityBufferDebug.shader")]
        private Shader _visibilityBufferDebugPS;

        [SerializeField]
        [ResourcePath("Shaders/Debugging/GBufferDebug.shader")]
        private Shader _gBufferDebugPS;

        public Shader VisibilityBufferDebugPS
        {
            get => _visibilityBufferDebugPS;
            set => this.SetValueAndNotify(ref _visibilityBufferDebugPS, value, nameof(_visibilityBufferDebugPS));
        }

        public Shader GBufferDebugPS
        {
            get => _gBufferDebugPS;
            set => this.SetValueAndNotify(ref _gBufferDebugPS, value, nameof(_gBufferDebugPS));
        }

        public int version => _version;

        bool IRenderPipelineGraphicsSettings.isAvailableInPlayerBuild =>
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            true;
#else
            false;
#endif
    }
}