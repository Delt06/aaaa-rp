using System;
using DELTation.AAAARP.Data;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Volumes
{
    [Serializable] [VolumeComponentMenu("AAAA/Screen-Space Reflections")] [SupportedOnRenderPipeline(typeof(AAAARenderPipelineAsset))]
    public class AAAASsrVolumeComponent : VolumeComponent
    {
        public enum ResolutionScale
        {
            Full = 1,
            Half = 2,
            Quarter = 4,
        }

        public BoolParameter Enabled = new(false);
        public EnumParameter<ResolutionScale> Resolution = new(ResolutionScale.Full);
        public ClampedFloatParameter BlurSmooth = new(0.5f, 0.0f, 10.0f);
        public ClampedFloatParameter BlurRough = new(2.5f, 0.0f, 10.0f);
        public ClampedFloatParameter MaxThickness = new(0.015f, 0.0f, 1.0f);

        public AAAASsrVolumeComponent() => displayName = "AAAA Screen-Space Reflections";
    }
}