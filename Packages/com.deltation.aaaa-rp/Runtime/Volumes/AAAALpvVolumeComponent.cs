using System;
using DELTation.AAAARP.Data;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Volumes
{
    [Serializable] [VolumeComponentMenu("AAAA/Lighting/Light Propagation Volumes")] [SupportedOnRenderPipeline(typeof(AAAARenderPipelineAsset))]
    public class AAAALpvVolumeComponent : VolumeComponent
    {
        public enum GridSizePreset
        {
            _16 = 16,
            _32 = 32,
            _64 = 64,
        }

        public enum QualityPreset
        {
            Low,
            Medium,
            High,
            Ultra,
        }

        public BoolParameter Enabled = new(false);
        public EnumParameter<GridSizePreset> GridSize = new(GridSizePreset._32);
        public EnumParameter<QualityPreset> InjectQuality = new(QualityPreset.Medium);
        public ClampedFloatParameter Intensity = new(1.0f, 0.0f, 20.0f);
        public ClampedIntParameter PropagationPasses = new(8, 0, 32);

        public AAAALpvVolumeComponent() => displayName = "AAAA Light Propagation Volumes";
    }
}