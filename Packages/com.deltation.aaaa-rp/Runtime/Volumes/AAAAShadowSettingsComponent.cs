using System;
using DELTation.AAAARP.Data;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Volumes
{
    [Serializable] [VolumeComponentMenu("AAAA/Shadow Settings")] [SupportedOnRenderPipeline(typeof(AAAARenderPipelineAsset))]
    public class AAAAShadowSettingsComponent : VolumeComponent
    {
        private const int MaxCascades = AAAALightingSettings.ShadowSettings.MaxCascades;
        private const float DefaultMaxDistance = AAAALightingSettings.ShadowSettings.DefaultMaxDistance;
        private const float DefaultShadowFade = AAAALightingSettings.ShadowSettings.DefaultShadowFade;

        public EnumParameter<AAAATextureSize> Resolution = new(AAAATextureSize._1024);

        public MinFloatParameter MaxDistance = new(DefaultMaxDistance, 1.0f);
        public ClampedIntParameter DirectionalLightCascades = new(MaxCascades, 1, MaxCascades);
        public ClampedFloatParameter DirectionalLightCascadeDistance1 = new(0.25f, 0.0f, 1.0f);
        public ClampedFloatParameter DirectionalLightCascadeDistance2 = new(0.5f, 0.0f, 1.0f);
        public ClampedFloatParameter DirectionalLightCascadeDistance3 = new(0.75f, 0.0f, 1.0f);
        public ClampedFloatParameter ShadowFade = new(DefaultShadowFade, 0.0f, 1.0f);

        public AAAAShadowSettingsComponent() => displayName = "AAAA Shadow Settings";
    }
}