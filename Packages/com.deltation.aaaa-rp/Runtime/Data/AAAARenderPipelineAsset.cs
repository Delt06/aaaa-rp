using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Data
{
    public enum AAAATextureSize
    {
        _16 = 16,
        _32 = 32,
        _64 = 64,
        _128 = 128,
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
        _2048 = 2048,
    }

    [Serializable]
    public class AAAAMeshLODSettings
    {
        [Min(0.0f)]
        public float ErrorThreshold = 50.0f;
    }

    [Serializable]
    public class AAAAImageBasedLightingSettings
    {
        public AAAATextureSize DiffuseIrradianceResolution = AAAATextureSize._128;
    }

    [CreateAssetMenu(menuName = "AAAA RP/AAAA Render Pipeline Asset")]
    public sealed class AAAARenderPipelineAsset : RenderPipelineAsset<AAAARenderPipeline>, IRenderGraphEnabledRenderPipeline
    {
        [SerializeField]
        private AAAAMeshLODSettings _meshLODSettings = new();
        [SerializeField]
        private AAAAImageBasedLightingSettings _imageBasedLightingSettings;

        public AAAAMeshLODSettings MeshLODSettings => _meshLODSettings;

        public AAAAImageBasedLightingSettings ImageBasedLightingSettings => _imageBasedLightingSettings;

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