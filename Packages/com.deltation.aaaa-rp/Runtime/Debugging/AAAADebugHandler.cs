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
        [CanBeNull]
        private readonly GBufferDebugPass _gBufferDebugPass;
        [CanBeNull]
        private readonly GPUCullingDebugReadbackPass _gpuCullingDebugReadbackPass;
        [CanBeNull]
        private readonly GPUCullingDebugSetupPass _gpuCullingDebugSetupPass;
        [CanBeNull]
        private readonly GPUCullingDebugViewPass _gpuCullingDebugViewPass;
        [CanBeNull]
        private readonly VisibilityBufferDebugPass _visibilityBufferDebugPass;

        public AAAADebugHandler()
        {
            if (GraphicsSettings.TryGetRenderPipelineSettings(out AAAARenderPipelineDebugShaders debugShaders))
            {
                _visibilityBufferDebugPass = new VisibilityBufferDebugPass(AAAARenderPassEvent.AfterRenderingTransparents, debugShaders, DisplaySettings);
                _gBufferDebugPass = new GBufferDebugPass(AAAARenderPassEvent.AfterRenderingTransparents, debugShaders, DisplaySettings);

                if (GraphicsSettings.TryGetRenderPipelineSettings(out AAAARenderPipelineRuntimeShaders runtimeShaders))
                {
                    _gpuCullingDebugSetupPass = new GPUCullingDebugSetupPass(AAAARenderPassEvent.BeforeRendering, runtimeShaders);
                    _gpuCullingDebugViewPass = new GPUCullingDebugViewPass(AAAARenderPassEvent.AfterRenderingTransparents, debugShaders, DisplaySettings);
                    _gpuCullingDebugReadbackPass = new GPUCullingDebugReadbackPass(AAAARenderPassEvent.AfterRendering);
                }
            }
        }

        public AAAARenderPipelineDebugDisplaySettings DisplaySettings { get; } = AAAARenderPipelineDebugDisplaySettings.Instance;

        public bool AreAnySettingsActive => DisplaySettings.AreAnySettingsActive;

        public void Dispose()
        {
            _visibilityBufferDebugPass?.Dispose();
        }

        [CanBeNull] public Camera GetGPUCullingCameraOverride()
        {
            if (!DisplaySettings.RenderingSettings.ForceCullingFromMainCamera)
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
                DisplaySettings.RenderingSettings.GetOverridenVisibilityBufferDebugMode() != AAAAVisibilityBufferDebugMode.None)
            {
                renderer.EnqueuePass(_visibilityBufferDebugPass);
            }

            if (_gBufferDebugPass != null &&
                DisplaySettings.RenderingSettings.GBufferDebugMode != AAAAGBufferDebugMode.None)
            {
                renderer.EnqueuePass(_gBufferDebugPass);
            }

            if (_gpuCullingDebugSetupPass != null &&
                _gpuCullingDebugViewPass != null &&
                DisplaySettings.RenderingSettings.DebugGPUCulling)
            {
                renderer.EnqueuePass(_gpuCullingDebugSetupPass);
                renderer.EnqueuePass(_gpuCullingDebugViewPass);

                if (_gpuCullingDebugReadbackPass != null)
                {
                    renderer.EnqueuePass(_gpuCullingDebugReadbackPass);
                }
            }
        }
    }
}