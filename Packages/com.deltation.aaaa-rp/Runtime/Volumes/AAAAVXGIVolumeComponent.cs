using System;
using DELTation.AAAARP.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Volumes
{
    [Serializable] [VolumeComponentMenu("AAAA/Lighting/Voxel Global Illumination")] [SupportedOnRenderPipeline(typeof(AAAARenderPipelineAsset))]
    public class AAAAVXGIVolumeComponent : VolumeComponent
    {
        public enum GridSizePreset
        {
            _16 = 16,
            _32 = 32,
            _64 = 64,
            _128 = 128,
        }

        public enum RenderScalePreset
        {
            Full = 1,
            Half = 2,
            Quarter = 4,
        }

        public BoolParameter Enabled = new(false);

        [Header("Grid")]
        public EnumParameter<GridSizePreset> GridSize = new(GridSizePreset._64);

        [Header("Lighting")]
        public EnumParameter<RenderScalePreset> RenderScale = new(RenderScalePreset.Half);
        public ClampedFloatParameter OpacityFactor = new(1.5f, 0.0f, 10.0f);

        public AAAAVXGIVolumeComponent() => displayName = "AAAA Voxel Global Illumination";
    }
}