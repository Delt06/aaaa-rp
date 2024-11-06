using System;
using DELTation.AAAARP.Data;
using UnityEngine.Rendering;
using static DELTation.AAAARP.Data.AAAALightingSettings.ShadowSettings;

namespace DELTation.AAAARP.Volumes
{
    [Serializable] [VolumeComponentMenu("AAAA/Lighting/Shadow Settings")] [SupportedOnRenderPipeline(typeof(AAAARenderPipelineAsset))]
    public class AAAAShadowSettingsComponent : VolumeComponent
    {
        private const int MaxCascades = AAAALightingSettings.ShadowSettings.MaxCascades;

        public EnumParameter<AAAATextureSize> Resolution = new(AAAATextureSize._1024);

        public MinFloatParameter MaxDistance = new(DefaultMaxDistance, 1.0f);
        public ClampedIntParameter DirectionalLightCascades = new(MaxCascades, 1, MaxCascades);
        public ClampedFloatParameter DirectionalLightCascadeDistance1 = new(0.25f, 0.0f, 1.0f);
        public ClampedFloatParameter DirectionalLightCascadeDistance2 = new(0.5f, 0.0f, 1.0f);
        public ClampedFloatParameter DirectionalLightCascadeDistance3 = new(0.75f, 0.0f, 1.0f);
        public ClampedFloatParameter DepthBias = new(DefaultDepthBias, 0.0f, 1.0f);
        public ClampedFloatParameter PunctualDepthBias = new(DefaultPunctualDepthBias, 0.0f, 1.0f);
        public ClampedFloatParameter SlopeBias = new(DefaultSlopeBias, 0.0f, 1.0f);
        public ClampedFloatParameter ShadowFade = new(DefaultShadowFade, 0.0f, 1.0f);

        public AAAAShadowSettingsComponent() => displayName = "AAAA Shadow Settings";
    }
}