using System;
using DELTation.AAAARP.RenderPipelineResources;
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

    public enum AAAAUpscalingTechnique
    {
        Off,
        FSR1,
    }

    public enum AAAAAmbientOcclusionTechnique
    {
        Off,
        XeGTAO,
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

        [EnumButtons]
        public AAAAAntiAliasingTechnique AntiAliasing;
        public SMAASettings SMAA = new();

        [EnumButtons]
        public AAAAUpscalingTechnique Upscaling;

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
        public enum XeGTAODenoisingLevel
        {
            Disabled = 0,
            Sharp = 1,
            Medium = 2,
            Soft = 3,
        }

        public enum XeGTAOQualityLevel
        {
            Low,
            Medium,
            High,
            Ultra,
        }

        public enum XeGTAOResolution
        {
            Full = 1,
            Half = 2,
            Quarter = 4,
        }

        [Range(16, 1024 * 16)]
        public int MaxPunctualLights = 1024;
        public ShadowSettings Shadows = new();

        [EnumButtons]
        public AAAAAmbientOcclusionTechnique AmbientOcclusion = AAAAAmbientOcclusionTechnique.XeGTAO;
        public XeGTAOSettings GTAOSettings = new();

        public SSRSettings SSR = new();

        [Serializable]
        public class SSRSettings
        {
            public enum ResolutionScale
            {
                Full = 1,
                Half = 2,
                Quarter = 4,
            }

            public bool Enabled = true;
            public ResolutionScale Resolution = ResolutionScale.Full;
            [Range(0.0f, 10.0f)]
            public float BlurSmooth = 0.5f;
            [Range(0.0f, 10.0f)]
            public float BlurRough = 2.5f;
            [Range(0.0f, 1.0f)]
            public float MaxThickness = 0.5f;
        }

        [Serializable]
        public class ShadowSettings
        {
            public AAAATextureSize Resolution = AAAATextureSize._1024;
            [Range(16, 512)]
            public int MaxShadowLightSlices = 128;
            [Min(1.0f)]
            public float MaxDistance = 100.0f;
            [Range(1, 4)]
            public int DirectionalLightCascades = 4;
            [Range(0.0f, 1.0f)] public float DirectionalLightCascadeDistance1 = 0.25f;
            [Range(0.0f, 1.0f)] public float DirectionalLightCascadeDistance2 = 0.5f;
            [Range(0.0f, 1.0f)] public float DirectionalLightCascadeDistance3 = 0.75f;
            [Range(0.0f, 1.0f)]
            public float ShadowFade = 0.2f;
            [Range(0.0f, 1.0f)]
            public float SlopeBias = 0.5f;
        }

        [Serializable]
        public class XeGTAOSettings
        {
            public XeGTAOResolution Resolution = XeGTAOResolution.Full;
            public XeGTAOQualityLevel QualityLevel = XeGTAOQualityLevel.High;
            public XeGTAODenoisingLevel DenoisingLevel = XeGTAODenoisingLevel.Sharp;
            public bool BentNormals;
            public bool DirectLightingMicroshadows;
            [Range(0.0f, 5.0f)]
            public float FinalValuePower = 1.0f;
            [Range(0.0f, 10.0f)]
            public float FalloffRange = 1.0f;
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

        public AAAAImageQualitySettings ImageQualitySettings => _imageQualitySettings;

        public AAAAMeshLODSettings MeshLODSettings => _meshLODSettings;

        public AAAALightingSettings LightingSettings => _lightingSettings;

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