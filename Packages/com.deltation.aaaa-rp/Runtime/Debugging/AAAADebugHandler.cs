﻿using System;
using DELTation.AAAARP.Data;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Passes;
using DELTation.AAAARP.Passes.Debugging;
using DELTation.AAAARP.RenderPipelineResources;
using DELTation.AAAARP.Utils;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using Object = UnityEngine.Object;

namespace DELTation.AAAARP.Debugging
{
    internal sealed class AAAADebugHandler : IDebugDisplaySettingsQuery, IDisposable
    {
        private readonly DebugSetupPass _debugSetupPass;
        [CanBeNull]
        private readonly GBufferDebugPass _gBufferDebugPass;
        [CanBeNull]
        private readonly GPUCullingDebugReadbackPass _gpuCullingDebugReadbackPass;
        [CanBeNull]
        private readonly GPUCullingDebugSetupPass _gpuCullingDebugSetupPass;
        [CanBeNull]
        private readonly GPUCullingDebugViewPass _gpuCullingDebugViewPass;
        [CanBeNull]
        private readonly LightingDebugPass _lightingDebugPass;
        [CanBeNull]
        private readonly VisibilityBufferDebugPass _visibilityBufferDebugPass;
        [CanBeNull]
        private readonly VXGIDebugPass _vxgiDebugPass;

        public AAAADebugHandler(AAAARawBufferClear rawBufferClear)
        {
            _debugSetupPass = new DebugSetupPass(AAAARenderPassEvent.BeforeRendering);

            if (GraphicsSettings.TryGetRenderPipelineSettings(out AAAARenderPipelineDebugShaders debugShaders))
            {
                _visibilityBufferDebugPass = new VisibilityBufferDebugPass(AAAARenderPassEvent.AfterRenderingTransparents, debugShaders, DisplaySettings);
                _gBufferDebugPass = new GBufferDebugPass(AAAARenderPassEvent.AfterRenderingTransparents, debugShaders, DisplaySettings);
                _lightingDebugPass = new LightingDebugPass(AAAARenderPassEvent.AfterRenderingTransparents, debugShaders, DisplaySettings);
                _vxgiDebugPass = new VXGIDebugPass(AAAARenderPassEvent.AfterRenderingTransparents, debugShaders, DisplaySettings);

                _gpuCullingDebugSetupPass = new GPUCullingDebugSetupPass(AAAARenderPassEvent.BeforeRendering, rawBufferClear);
                _gpuCullingDebugViewPass = new GPUCullingDebugViewPass(AAAARenderPassEvent.AfterRenderingTransparents, debugShaders, DisplaySettings);
                _gpuCullingDebugReadbackPass = new GPUCullingDebugReadbackPass(AAAARenderPassEvent.AfterRendering, DisplaySettings);
            }
        }

        public AAAARenderPipelineDebugDisplaySettings DisplaySettings { get; } = AAAARenderPipelineDebugDisplaySettings.Instance;

        public bool AreAnySettingsActive => DisplaySettings.AreAnySettingsActive;

        public void Dispose()
        {
            _visibilityBufferDebugPass?.Dispose();
            _gBufferDebugPass?.Dispose();
            _lightingDebugPass?.Dispose();
            _vxgiDebugPass?.Dispose();
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

        public void Setup(AAAARendererBase renderer, RenderGraph renderGraph, ScriptableRenderContext context, ContextContainer frameData)
        {
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();

            if (DisplaySettings.AreAnySettingsActive)
            {
                renderer.EnqueuePass(_debugSetupPass);
            }

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

            if (_lightingDebugPass != null &&
                DisplaySettings.RenderingSettings.LightingDebugMode != AAAALightingDebugMode.None)
            {
                renderer.EnqueuePass(_lightingDebugPass);
            }

            if (_vxgiDebugPass != null &&
                DisplaySettings.RenderingSettings.VXGIDebug &&
                cameraData.RealtimeGITechnique is AAAARealtimeGITechnique.Voxel)
            {
                renderer.EnqueuePass(_vxgiDebugPass);
            }

            if (_gpuCullingDebugSetupPass != null &&
                DisplaySettings.RenderingSettings.DebugGPUCulling)
            {
                renderer.EnqueuePass(_gpuCullingDebugSetupPass);

                if (_gpuCullingDebugViewPass != null && DisplaySettings.RenderingSettings.GPUCullingDebugViewMode != AAAAGPUCullingDebugViewMode.None)
                {
                    renderer.EnqueuePass(_gpuCullingDebugViewPass);
                }

                if (_gpuCullingDebugReadbackPass != null && DisplaySettings.RenderingSettings.DebugGPUCulling)
                {
                    renderer.EnqueuePass(_gpuCullingDebugReadbackPass);
                }
            }
        }
    }
}