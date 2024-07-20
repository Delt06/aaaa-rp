using System;
using DELTation.AAAARP.Passes;
using DELTation.AAAARP.Passes.Debugging;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using Object = UnityEngine.Object;

namespace DELTation.AAAARP.Debugging
{
    internal sealed class AAAADebugHandler : IDebugDisplaySettingsQuery, IDisposable
    {
        private readonly AAAARenderPipelineDebugDisplaySettings _debugDisplaySettings = AAAARenderPipelineDebugDisplaySettings.Instance;
        [CanBeNull]
        private readonly GBufferDebugPass _gBufferDebugPass;

        [CanBeNull]
        private readonly VisibilityBufferDebugPass _visibilityBufferDebugPass;

        public AAAADebugHandler()
        {
            if (GraphicsSettings.TryGetRenderPipelineSettings(out AAAARenderPipelineDebugShaders shaders))
            {
                _visibilityBufferDebugPass = new VisibilityBufferDebugPass(AAAARenderPassEvent.AfterRenderingTransparents, shaders, _debugDisplaySettings);
                _gBufferDebugPass = new GBufferDebugPass(AAAARenderPassEvent.AfterRenderingTransparents, shaders, _debugDisplaySettings);
            }
        }

        public bool AreAnySettingsActive => _debugDisplaySettings.AreAnySettingsActive;

        public void Dispose()
        {
            _visibilityBufferDebugPass?.Dispose();
        }

        [CanBeNull] public Camera GetGPUCullingCameraOverride()
        {
            if (!_debugDisplaySettings.RenderingSettings.ForceCullingFromMainCamera)
            {
                return null;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return mainCamera;
            }

            return Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);
        }

        public void Setup(AAAARendererBase renderer, RenderGraph renderGraph, ScriptableRenderContext context)
        {
            if (_visibilityBufferDebugPass != null &&
                _debugDisplaySettings.RenderingSettings.GetOverridenVisibilityBufferDebugMode() != AAAAVisibilityBufferDebugMode.None)
            {
                renderer.EnqueuePass(_visibilityBufferDebugPass);
            }

            if (_gBufferDebugPass != null &&
                _debugDisplaySettings.RenderingSettings.GBufferDebugMode != AAAAGBufferDebugMode.None)
            {
                renderer.EnqueuePass(_gBufferDebugPass);
            }
        }
    }
}