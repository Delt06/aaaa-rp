﻿using System;
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

        public BoolParameter Enabled = new(false);
        public BoolParameter BlockingPotential = new(true);
        public EnumParameter<GridSizePreset> GridSize = new(GridSizePreset._32);
        public ClampedFloatParameter BoundsSize = new(40.0f, 1.0f, 100.0f);
        public ClampedFloatParameter BoundsForwardBias = new(0.5f, 0.0f, 1.0f);
        public ClampedFloatParameter Intensity = new(1.0f, 0.0f, 20.0f);
        public ClampedFloatParameter InjectionDepthBias = new(0.25f, 0.0f, 5.0f);
        public ClampedFloatParameter InjectionNormalBias = new(0.25f, 0.0f, 5.0f);
        public ClampedFloatParameter OcclusionAmplification = new(5.0f, 0.0f, 10.0f);
        public ClampedIntParameter PropagationPasses = new(8, 0, 32);

        public AAAALpvVolumeComponent() => displayName = "AAAA Light Propagation Volumes";
    }
}