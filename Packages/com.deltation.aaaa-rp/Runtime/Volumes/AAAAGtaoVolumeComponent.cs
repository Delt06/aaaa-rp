using System;
using DELTation.AAAARP.Data;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Volumes
{
    [Serializable] [VolumeComponentMenu("AAAA/Ground Truth Ambient Occlusion")] [SupportedOnRenderPipeline(typeof(AAAARenderPipelineAsset))]
    public class AAAAGtaoVolumeComponent : VolumeComponent
    {
        public ClampedFloatParameter FinalValuePower = new(1.0f, 0.0f, 5.0f);
        public ClampedFloatParameter FalloffRange = new(0.1f, 0.0f, 10.0f);

        public AAAAGtaoVolumeComponent() => displayName = "AAAA Ground Truth Ambient Occlusion";
    }
}