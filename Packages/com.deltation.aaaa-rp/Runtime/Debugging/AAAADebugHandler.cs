using System;
using DELTation.AAAARP.Passes;
using DELTation.AAAARP.Passes.Debugging;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Debugging
{
    internal sealed class AAAADebugHandler : IDebugDisplaySettingsQuery, IDisposable
    {
        private readonly AAAARenderPipelineDebugDisplaySettings _debugDisplaySettings = AAAARenderPipelineDebugDisplaySettings.Instance;
        private readonly VisibilityBufferDebugPass _visibilityBufferDebugPass;

        public AAAADebugHandler()
        {
            if (GraphicsSettings.TryGetRenderPipelineSettings(out AAAARenderPipelineDebugShaders shaders))
            {
                _visibilityBufferDebugPass = new VisibilityBufferDebugPass(AAAARenderPassEvent.AfterRenderingTransparents, shaders, _debugDisplaySettings);
            }
        }

        public bool AreAnySettingsActive => _debugDisplaySettings.AreAnySettingsActive;

        public void Dispose()
        {
            _visibilityBufferDebugPass.Dispose();
        }

        public void Setup(AAAARendererBase renderer, RenderGraph renderGraph, ScriptableRenderContext context)
        {
            if (_debugDisplaySettings.RenderingSettings.VisibilityBufferDebugMode != AAAAVisibilityBufferDebugMode.None)
            {
                renderer.EnqueuePass(_visibilityBufferDebugPass);
            }
        }
    }
}