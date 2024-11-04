using System;
using DELTation.AAAARP.Data;
using DELTation.AAAARP.Passes.PostProcessing;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Volumes
{
    [Serializable] [VolumeComponentMenu("AAAA/Post-Processing/FSR Sharpness")] [SupportedOnRenderPipeline(typeof(AAAARenderPipelineAsset))]
    public class AAAAFsrSharpnessVolumeComponent : VolumeComponent
    {
        public ClampedFloatParameter Sharpness = new(0.0f, 0.0f, FSRPass.MaxSharpness);

        public AAAAFsrSharpnessVolumeComponent() => displayName = "AAAA FSR Sharpness";
    }
}