using System;
using DELTation.AAAARP.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Volumes
{
    [Serializable] [VolumeComponentMenu("AAAA/Post-Processing/Post Processing Options")] [SupportedOnRenderPipeline(typeof(AAAARenderPipelineAsset))]
    public class AAAAPostProcessingOptionsVolumeComponent : VolumeComponent
    {
        public ClampedFloatParameter Exposure = new(1.0f, 0.1f, 10.0f);
        public EnumParameter<AAAAToneMappingProfile> ToneMappingProfile = new(AAAAToneMappingProfile.Off);

        public AAAAPostProcessingOptionsVolumeComponent() => displayName = "AAAA Post Processing Options";

        public bool AnyEnabled() =>
            !Mathf.Approximately(Exposure.value, 1.0f) ||
            ToneMappingProfile.value != AAAAToneMappingProfile.Off;
    }
}