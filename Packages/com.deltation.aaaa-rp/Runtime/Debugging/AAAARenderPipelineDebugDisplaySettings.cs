using UnityEngine.Rendering;

namespace DELTation.AAAARP.Debugging
{
    public sealed class AAAARenderPipelineDebugDisplaySettings : DebugDisplaySettings<AAAARenderPipelineDebugDisplaySettings>
    {
        public AAAADebugStats DebugStats { get; private set; }
        public AAAADebugDisplaySettingsRendering RenderingSettings { get; private set; }

        public override bool AreAnySettingsActive => RenderingSettings.AreAnySettingsActive;

        public override void Reset()
        {
            base.Reset();
            DebugStats = new AAAADebugStats();
            RenderingSettings = Add(new AAAADebugDisplaySettingsRendering(DebugStats));
        }
    }
}