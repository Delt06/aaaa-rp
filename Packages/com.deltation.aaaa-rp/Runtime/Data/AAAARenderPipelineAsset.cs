﻿using System;
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
        _4096 = 4096,
        _8192 = 8192,
    }

    public enum AAAAAntiAliasingTechnique
    {
        Off,
        SMAA,
    }

    [Serializable]
    public class AAAAImageQualitySettings
    {
        public enum SMAAPreset
        {
            Low,
            Medium,
            High,
            Ultra,
        }

        [Range(0.5f, 2.0f)]
        public float RenderScale = 1.0f;
        public AAAAAntiAliasingTechnique AntiAliasing;
        public SMAASettings SMAA = new();

        [Serializable]
        public class SMAASettings
        {
            public SMAAPreset Preset = SMAAPreset.High;
        }
    }

    [Serializable]
    public class AAAAMeshLODSettings
    {
        [Min(0.0f)]
        public float ErrorThreshold = 50.0f;
    }

    [Serializable]
    public class AAAALightingSettings
    {
        [Range(16, 1024 * 16)]
        public int MaxPunctualLights = 1024;
        public ShadowSettings Shadows = new();

        [Serializable]
        public class ShadowSettings
        {
            public AAAATextureSize Resolution = AAAATextureSize._1024;
            [Range(16, 512)]
            public int MaxShadowLightSlices = 128;
            [Min(1.0f)]
            public float MaxDistance = 100.0f;
            [Range(0.0f, 1.0f)]
            public float SlopeBias = 0.5f;
        }
    }

    [Serializable]
    public class AAAAImageBasedLightingSettings
    {
        public AAAATextureSize DiffuseIrradianceResolution = AAAATextureSize._128;

        public AAAATextureSize BRDFLutResolution = AAAATextureSize._128;

        public PreFilteredEnvironmentMapSettings PreFilteredEnvironmentMap = new();

        [Serializable]
        public class PreFilteredEnvironmentMapSettings
        {
            public AAAATextureSize Resolution = AAAATextureSize._256;
            [Range(2, 10)] public int MaxMipLevels = 5;
        }
    }

    [Serializable]
    public class AAAAPostProcessingSettings
    {
        public enum ToneMappingProfile
        {
            Off = 0,
            Neutral,
            ACES,
        }

        public ToneMappingProfile ToneMapping = ToneMappingProfile.Off;

        public bool AnyEnabled() => ToneMapping != ToneMappingProfile.Off;
    }

    [CreateAssetMenu(menuName = "AAAA RP/AAAA Render Pipeline Asset")]
    public sealed class AAAARenderPipelineAsset : RenderPipelineAsset<AAAARenderPipeline>, IRenderGraphEnabledRenderPipeline
    {
        [SerializeField]
        private AAAAImageQualitySettings _imageQualitySettings = new();
        [SerializeField]
        private AAAAMeshLODSettings _meshLODSettings = new();
        [SerializeField]
        private AAAALightingSettings _lightingSettings = new();
        [SerializeField]
        private AAAAImageBasedLightingSettings _imageBasedLightingSettings = new();
        [SerializeField]
        private AAAAPostProcessingSettings _postProcessingSettings = new();

        public AAAAImageQualitySettings ImageQualitySettings => _imageQualitySettings;

        public AAAAMeshLODSettings MeshLODSettings => _meshLODSettings;

        public AAAALightingSettings LightingSettings => _lightingSettings;

        public AAAAImageBasedLightingSettings ImageBasedLightingSettings => _imageBasedLightingSettings;

        public AAAAPostProcessingSettings PostProcessingSettings => _postProcessingSettings;

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