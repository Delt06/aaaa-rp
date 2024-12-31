using System;
using DELTation.AAAARP.Data;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Volumes
{
    [Serializable] [VolumeComponentMenu("AAAA/Lighting/Voxel Global Illumination")] [SupportedOnRenderPipeline(typeof(AAAARenderPipelineAsset))]
    public class AAAAVXGIVolumeComponent : VolumeComponent
    {
        public BoolParameter Enabled = new(false);

        public ClampedFloatParameter OpacityFactor = new(1.5f, 0.0f, 10.0f);

        public AAAAVXGIVolumeComponent() => displayName = "AAAA Voxel Global Illumination";
    }
}