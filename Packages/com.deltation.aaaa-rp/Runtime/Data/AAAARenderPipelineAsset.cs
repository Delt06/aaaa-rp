using System;
using DELTation.AAAARP.RenderPipelineResources;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

    public enum AAAARealtimeGITechnique
    {
        Off,
        LightPropagationVolumes,
        Voxel,
    }

    [Serializable]
    public class AAAAImageQualitySettings
    {
        [Range(0.5f, 2.0f)]
        public float RenderScale = 1.0f;

        [EnumButtons]
        public AAAAUpscalingTechnique Upscaling;
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
        public enum LightProbeSystem
        {
            Off,
            AdaptiveProbeVolumes,
        }

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

        [EnumButtons] public AAAAAmbientOcclusionTechnique AmbientOcclusion = AAAAAmbientOcclusionTechnique.XeGTAO;
        public XeGTAOSettings GTAOSettings = new();
        [EnumButtons] public LightProbeSystem LightProbes = LightProbeSystem.AdaptiveProbeVolumes;
        public ProbeVolumesSettings ProbeVolumes = new();
        [EnumButtons] public AAAARealtimeGITechnique RealtimeGI = AAAARealtimeGITechnique.Off;

        [Serializable]
        public class ShadowSettings
        {
            public const float DefaultMaxDistance = 100.0f;
            public const int MaxCascades = 4;
            public const float DefaultDepthBias = 0.1f;
            public const float DefaultPunctualDepthBias = 0.05f;
            public const float DefaultSlopeBias = 0.5f;
            public const float DefaultShadowFade = 0.2f;

            public AAAATextureSize Resolution = AAAATextureSize._1024;
            [Range(16, 512)] public int MaxShadowLightSlices = 128;
            [Min(1.0f)] public float MaxDistance = DefaultMaxDistance;
            [Range(1, MaxCascades)] public int DirectionalLightCascades = MaxCascades;
            [Range(0.0f, 1.0f)] public float DirectionalLightCascadeDistance1 = 0.25f;
            [Range(0.0f, 1.0f)] public float DirectionalLightCascadeDistance2 = 0.5f;
            [Range(0.0f, 1.0f)] public float DirectionalLightCascadeDistance3 = 0.75f;
            [Range(0.0f, 1.0f)] public float ShadowFade = DefaultShadowFade;
            [Range(0.0f, 1.0f)] public float DepthBias = DefaultDepthBias;
            [Range(0.0f, 1.0f)] public float PunctualDepthBias = DefaultPunctualDepthBias;
            [Range(0.0f, 1.0f)] public float SlopeBias = DefaultSlopeBias;
        }

        [Serializable]
        public class XeGTAOSettings
        {
            public XeGTAOResolution Resolution = XeGTAOResolution.Full;
            public XeGTAOQualityLevel QualityLevel = XeGTAOQualityLevel.High;
            public XeGTAODenoisingLevel DenoisingLevel = XeGTAODenoisingLevel.Sharp;
            public bool BentNormals;
            public bool DirectLightingMicroshadows;
        }

        [Serializable]
        public class ProbeVolumesSettings
        {
            public ProbeVolumeTextureMemoryBudget MemoryBudget = ProbeVolumeTextureMemoryBudget.MemoryBudgetMedium;
            public ProbeVolumeBlendingTextureMemoryBudget BlendingMemoryBudget = ProbeVolumeBlendingTextureMemoryBudget.MemoryBudgetMedium;
            public ProbeVolumeSHBands SHBands = ProbeVolumeSHBands.SphericalHarmonicsL1;
            public bool SupportGPUStreaming;
            public bool SupportDiskStreaming;
            public bool SupportScenarios;
            public bool SupportScenarioBlending;
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
    public sealed class AAAARenderPipelineAsset : RenderPipelineAsset<AAAARenderPipeline>, IRenderGraphEnabledRenderPipeline, IProbeVolumeEnabledRenderPipeline
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

        public override Shader defaultShader => Shader.Find("AAAA/Lit");
        public override Material defaultMaterial
        {
            get
            {
#if UNITY_EDITOR
                return AssetDatabase.LoadAssetAtPath<Material>("Packages/com.deltation.aaaa-rp/Assets/Materials/AAAA Lit.mat");
#else
                return new Material(defaultShader);
#endif
            }
        }

        bool IProbeVolumeEnabledRenderPipeline.supportProbeVolume => LightingSettings.LightProbes == AAAALightingSettings.LightProbeSystem.AdaptiveProbeVolumes;

        ProbeVolumeSHBands IProbeVolumeEnabledRenderPipeline.maxSHBands =>
            LightingSettings.LightProbes == AAAALightingSettings.LightProbeSystem.AdaptiveProbeVolumes
                ? LightingSettings.ProbeVolumes.SHBands
                : ProbeVolumeSHBands.SphericalHarmonicsL1;

        [Obsolete("This property is no longer necessary.")]
        ProbeVolumeSceneData IProbeVolumeEnabledRenderPipeline.probeVolumeSceneData => null;

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