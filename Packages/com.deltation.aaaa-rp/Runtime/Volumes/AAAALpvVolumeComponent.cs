using System;
using DELTation.AAAARP.Data;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Volumes
{
    [Serializable] [VolumeComponentMenu("AAAA/Lighting/Light Propagation Volumes")] [SupportedOnRenderPipeline(typeof(AAAARenderPipelineAsset))]
    public class AAAALpvVolumeComponent : VolumeComponent
    {
        public BoolParameter Enabled = new(false);
        public ClampedFloatParameter Intensity = new(1.0f, 0.0f, 5.0f);
        public ClampedIntParameter PropagationPasses = new(8, 0, 32);

        public AAAALpvVolumeComponent() => displayName = "AAAA Light Propagation Volumes";
    }
}