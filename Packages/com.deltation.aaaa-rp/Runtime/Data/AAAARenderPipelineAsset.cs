using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Data
{
    [Serializable]
    public class AAAAMeshLODSettings
    {
        [Min(0.0f)]
        public float ErrorThreshold = 50.0f;
    }

    [CreateAssetMenu(menuName = "AAAA RP/AAAA Render Pipeline Asset")]
    public sealed class AAAARenderPipelineAsset : RenderPipelineAsset<AAAARenderPipeline>, IRenderGraphEnabledRenderPipeline
    {
        [SerializeField]
        private AAAAMeshLODSettings _meshLODSettings = new();

        public AAAAMeshLODSettings MeshLODSettings => _meshLODSettings;

        public override string renderPipelineShaderTag => AAAARenderPipeline.ShaderTagName;

        public bool isImmediateModeSupported => false;

        protected override RenderPipeline CreatePipeline() => new AAAARenderPipeline(this);

        protected override void EnsureGlobalSettings()
        {
            base.EnsureGlobalSettings();

#if UNITY_EDITOR
            AAAARenderPipelineGlobalSettings.Ensure();
#endif
        }
    }
}