using UnityEngine.Rendering;

namespace DELTation.AAAARP.Debugging
{
    public sealed class AAAARenderPipelineDebugDisplaySettings : DebugDisplaySettings<AAAARenderPipelineDebugDisplaySettings>
    {
        public AAAADebugDisplaySettingsRendering RenderingSettings { get; private set; }

        public override bool AreAnySettingsActive => RenderingSettings.AreAnySettingsActive;

        public override void Reset()
        {
            base.Reset();
            RenderingSettings = Add(new AAAADebugDisplaySettingsRendering());
        }
    }
}