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
            _256 = 256,
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
        public ClampedFloatParameter BoundsSize = new(40.0f, 1.0f, 100.0f);
        public ClampedFloatParameter BoundsForwardBias = new(0.5f, 0.0f, 1.0f);
        public ClampedFloatParameter BoundsVerticalForwardBias = new(0.5f, 0.0f, 1.0f);

        [Header("Lighting")]
        public BoolParameter IndirectSpecular = new(true);
        public EnumParameter<RenderScalePreset> RenderScale = new(RenderScalePreset.Half);
        public ClampedFloatParameter DiffuseOpacityFactor = new(1.0f, 0.0f, 10.0f);
        public ClampedFloatParameter SpecularOpacityFactor = new(1.0f, 0.0f, 10.0f);

        public AAAAVXGIVolumeComponent() => displayName = "AAAA Voxel Global Illumination";
    }
}