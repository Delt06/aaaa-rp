using System;
using System.Collections.Generic;
using DELTation.AAAARP.Data;
using DELTation.AAAARP.Debugging;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Renderers;
using DELTation.AAAARP.RenderPipelineResources;
using DELTation.AAAARP.Utils;
using DELTation.AAAARP.Volumes;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using static DELTation.AAAARP.AAAARenderPipelineCore;

namespace DELTation.AAAARP
{
    public sealed partial class AAAARenderPipeline : RenderPipeline
    {
        public const string ShaderTagName = "AAAAPipeline";
        private readonly bool _areAPVEnabled;
        private readonly BindlessTextureContainer _bindlessTextureContainer;
        private readonly DebugDisplaySettingsUI _debugDisplaySettingsUI = new();

        private readonly AAAARenderPipelineAsset _pipelineAsset;
        private readonly AAAARawBufferClear _rawBufferClear;
        private readonly AAAARendererBase _renderer;
        private readonly AAAARendererContainer _rendererContainer;

        private readonly AAAARenderTexturePoolSet _rtPoolSet;
        private RenderGraph _renderGraph;

        public AAAARenderPipeline(AAAARenderPipelineAsset pipelineAsset)
        {
            AAAARenderPipelineRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAARenderPipelineRuntimeShaders>();
            _rawBufferClear = new AAAARawBufferClear(shaders);

            _pipelineAsset = pipelineAsset;
            _renderer = new AAAARenderer(_rawBufferClear);
            _renderGraph = new RenderGraph("AAAARPRenderGraph");

            Blitter.Initialize(shaders.CoreBlitPS, shaders.CoreBlitColorAndDepthPS);

            DebugManager.instance.RefreshEditor();

            AAAARenderPipelineDebugDisplaySettings pipelineDebugDisplaySettings = null;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            pipelineDebugDisplaySettings = AAAARenderPipelineDebugDisplaySettings.Instance;
            _debugDisplaySettingsUI.RegisterDebug(pipelineDebugDisplaySettings);
#else
#endif

            RTHandles.Initialize(Screen.width, Screen.height);
            ShaderGlobalKeywords.InitializeShaderGlobalKeywords();

            AAAADefaultVolumeProfileSettings defaultVolumeProfileSettings = GraphicsSettings.GetRenderPipelineSettings<AAAADefaultVolumeProfileSettings>();
            VolumeManager.instance.Initialize(defaultVolumeProfileSettings.volumeProfile);

            _bindlessTextureContainer = new BindlessTextureContainer();
            _rtPoolSet = new AAAARenderTexturePoolSet(_bindlessTextureContainer);
            _rendererContainer =
                new AAAARendererContainer(_bindlessTextureContainer, pipelineAsset.MeshLODSettings, _rawBufferClear, pipelineDebugDisplaySettings);

            _areAPVEnabled = pipelineAsset.LightingSettings.LightProbes == AAAALightingSettings.LightProbeSystem.AdaptiveProbeVolumes;
            SupportedRenderingFeatures.active.overridesLightProbeSystem = _areAPVEnabled;
            SupportedRenderingFeatures.active.skyOcclusion = _areAPVEnabled;
            if (_areAPVEnabled)
            {
                AAAALightingSettings.ProbeVolumesSettings probeVolumesSettings = pipelineAsset.LightingSettings.ProbeVolumes;
                ProbeReferenceVolume.instance.Initialize(new ProbeVolumeSystemParameters
                    {
                        memoryBudget = probeVolumesSettings.MemoryBudget,
                        blendingMemoryBudget = probeVolumesSettings.BlendingMemoryBudget,
                        shBands = probeVolumesSettings.SHBands,
                        supportGPUStreaming = probeVolumesSettings.SupportGPUStreaming,
                        supportDiskStreaming = probeVolumesSettings.SupportDiskStreaming,
                        supportScenarios = probeVolumesSettings.SupportScenarios,
                        supportScenarioBlending = probeVolumesSettings.SupportScenarioBlending,
                    }
                );
            }
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            throw new InvalidOperationException("The variant with the List<> is used instead.");
        }

        protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
        {
            GraphicsSettings.lightsUseLinearIntensity = QualitySettings.activeColorSpace == ColorSpace.Linear;
            GraphicsSettings.lightsUseColorTemperature = true;
            GraphicsSettings.useScriptableRenderPipelineBatching = true;

            _rtPoolSet.OnPreRender();
            _rendererContainer.PreRender(context);

            foreach (Camera camera in cameras)
            {
                if (camera.cameraType == CameraType.Preview)
                {
                    // Don't support previews at the moment.
                    continue;
                }
                UpdateVolumeFramework(camera);
                RenderSingleCamera(context, _renderer, camera);
            }

            _rendererContainer.PostRender();

            _renderGraph.EndFrame();
        }

        private void RenderSingleCamera(ScriptableRenderContext context, AAAARendererBase renderer, Camera camera)
        {
            ContextContainer frameData = renderer.FrameData;
            AAAARenderingData renderingData = CreateRenderingData(frameData);
            AAAACameraData cameraData = CreateCameraData(frameData, camera, renderer, renderingData);
            CreateResourceData(frameData, cameraData);
            CreateRendererListData(frameData);
            CreateLightingData(frameData);
            CreateShadowsData(frameData);
            CreateImageBasedLightingData(frameData);

            RenderSingleCameraImpl(context, renderer, cameraData);
        }

        private void RenderSingleCameraImpl(ScriptableRenderContext context, AAAARendererBase renderer, AAAACameraData cameraData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();

            CameraMetadata cameraMetadata = CameraMetadataCache.Get(cameraData.Camera);

            using (new ProfilingScope(cmd, cameraMetadata.Sampler))
            {
                renderer.Clear(cameraData.Camera);

                switch (cameraData.CameraType)
                {
                    case CameraType.Reflection or CameraType.Preview:
                        ScriptableRenderContext.EmitGeometryForCamera(cameraData.Camera);
                        break;
#if UNITY_EDITOR
                    case CameraType.SceneView:
                        ScriptableRenderContext.EmitWorldGeometryForSceneView(cameraData.Camera);
                        break;
#endif
                }

                {
                    // Must be called before culling because it emits intermediate renderers via Graphics.DrawInstanced.
                    ProbeVolumesOptions apvOptions = cameraData.VolumeStack?.GetComponent<ProbeVolumesOptions>();
                    ProbeReferenceVolume.instance.RenderDebug(cameraData.Camera, apvOptions, Texture2D.whiteTexture);
                }

                RecordAndExecuteRenderGraph(_renderGraph, context, renderer, cmd, cameraMetadata.Name);
                renderer.FinishRenderGraphRendering(cmd);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            renderer.PostRender(context, cameraData.Camera);

            using (new ProfilingScope(AAAAProfiling.Pipeline.Context.Submit))
            {
                context.Submit();
            }
        }

        protected override void Dispose(bool disposing)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            _debugDisplaySettingsUI.UnregisterDebug();
#endif
            if (_areAPVEnabled)
            {
                ProbeReferenceVolume.instance.Cleanup();
            }

            _rendererContainer.Dispose();
            _rtPoolSet.Dispose();
            _bindlessTextureContainer.Dispose();

            Blitter.Cleanup();
            ConstantBuffer.ReleaseAll();
            VolumeManager.instance.Deinitialize();

            base.Dispose(disposing);

            _renderer?.Dispose();
            _renderGraph.Cleanup();
            _renderGraph = null;

            _rawBufferClear.Dispose();
        }

        private AAAAResourceData CreateResourceData(ContextContainer frameData, AAAACameraData cameraData)
        {
            AAAAResourceData resourceData = frameData.GetOrCreate<AAAAResourceData>();

            return resourceData;
        }

        private AAAARenderingData CreateRenderingData(ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.GetOrCreate<AAAARenderingData>();

            renderingData.PipelineAsset = _pipelineAsset;
            renderingData.RenderGraph = _renderGraph;
            renderingData.RendererContainer = _rendererContainer;
            renderingData.RtPoolSet = _rtPoolSet;

            return renderingData;
        }

        private static AAAACameraData CreateCameraData(ContextContainer frameData, Camera camera, AAAARendererBase renderer, AAAARenderingData renderingData)
        {
            AAAACameraData cameraData = frameData.GetOrCreate<AAAACameraData>();

            cameraData.Renderer = renderer;
            cameraData.Camera = camera;
            cameraData.AdditionalCameraData = AAAAAdditionalCameraData.GetOrAdd(camera);
            cameraData.IsHdrEnabled = camera.allowHDR;
            cameraData.CameraType = camera.cameraType;

            AAAAImageQualitySettings imageQualitySettings = cameraData.CameraType is CameraType.Game ? renderingData.PipelineAsset.ImageQualitySettings : null;
            AAAALightingSettings lightingSettings =
                cameraData.CameraType is CameraType.Game or CameraType.SceneView ? renderingData.PipelineAsset.LightingSettings : null;

            Rect cameraRect = camera.rect;
            cameraData.PixelRect = camera.pixelRect;
            cameraData.PixelWidth = camera.pixelWidth;
            cameraData.PixelHeight = camera.pixelHeight;
            cameraData.RenderScale = imageQualitySettings?.RenderScale ?? 1.0f;
            cameraData.AspectRatio = cameraData.PixelWidth / (float) cameraData.PixelHeight;
            cameraData.IsDefaultViewport = !(Mathf.Abs(cameraRect.x) > 0.0f || Mathf.Abs(cameraRect.y) > 0.0f ||
                                             Mathf.Abs(cameraRect.width) < 1.0f || Mathf.Abs(cameraRect.height) < 1.0f);
            cameraData.TargetTexture = camera.targetTexture;

            if (cameraData.CameraType == CameraType.SceneView)
            {
                cameraData.ClearDepth = true;
                cameraData.UseScreenCoordOverride = false;
                cameraData.ScreenSizeOverride = cameraData.PixelRect.size;
                cameraData.ScreenCoordScaleBias = Vector2.one;
            }
            else
            {
                cameraData.ClearDepth = camera.clearFlags != CameraClearFlags.Nothing;
                cameraData.UseScreenCoordOverride = false;
                cameraData.ScreenSizeOverride = cameraData.PixelRect.size;
                cameraData.ScreenCoordScaleBias = new Vector4(1, 1, 0, 0);
            }

            cameraData.ClearColor = camera.clearFlags == CameraClearFlags.Color;
            cameraData.BackgroundColor = camera.backgroundColor;

            ///////////////////////////////////////////////////////////////////
            // Descriptor settings                                            /
            ///////////////////////////////////////////////////////////////////
            bool needsAlphaChannel = Graphics.preserveFramebufferAlpha;

            cameraData.CameraTargetDescriptor = CreateRenderTextureDescriptor(camera, cameraData,
                cameraData.IsHdrEnabled, cameraData.HDRColorBufferPrecision, needsAlphaChannel
            );

            cameraData.IsAlphaOutputEnabled = GraphicsFormatUtility.HasAlphaChannel(cameraData.CameraTargetDescriptor.graphicsFormat);

            if (cameraData.Camera.cameraType == CameraType.SceneView && CoreUtils.IsSceneFilteringEnabled())
            {
                cameraData.IsAlphaOutputEnabled = true;
            }

            Matrix4x4 projectionMatrix = camera.projectionMatrix;
            cameraData.SetViewProjectionAndJitterMatrix(camera.worldToCameraMatrix, projectionMatrix);
            cameraData.WorldSpaceCameraPos = camera.transform.position;

            cameraData.VolumeStack = VolumeManager.instance.stack;
            cameraData.AntiAliasingTechnique =
                cameraData.CameraType is CameraType.Game ? cameraData.AdditionalCameraData.AntiAliasing : AAAAAntiAliasingTechnique.Off;
            cameraData.UpscalingTechnique = imageQualitySettings?.Upscaling ?? AAAAUpscalingTechnique.Off;
            cameraData.FSRSharpness = cameraData.VolumeStack.GetComponent<AAAAFsrSharpnessVolumeComponent>().Sharpness.value;
            if (cameraData.RenderScale > 1)
            {
                cameraData.FSRSharpness = 0.0f;
            }

            if (cameraData.RenderScale >= 1.0f && Mathf.Approximately(cameraData.FSRSharpness, 0.0f))
            {
                cameraData.UpscalingTechnique = AAAAUpscalingTechnique.Off;
            }
            cameraData.PostProcessingEnabled =
                cameraData.VolumeStack.GetComponent<AAAAPostProcessingOptionsVolumeComponent>().AnyEnabled();
            cameraData.AmbientOcclusionTechnique = lightingSettings?.AmbientOcclusion ?? AAAAAmbientOcclusionTechnique.Off;
            if (cameraData.AmbientOcclusionTechnique == AAAAAmbientOcclusionTechnique.XeGTAO)
            {
                AAAAGtaoVolumeComponent gtaoVolumeComponent = cameraData.VolumeStack.GetComponent<AAAAGtaoVolumeComponent>();
                if (gtaoVolumeComponent.Enabled.value == false || gtaoVolumeComponent.FinalValuePower.value == 0.0f)
                {
                    cameraData.AmbientOcclusionTechnique = AAAAAmbientOcclusionTechnique.Off;
                }
            }
            cameraData.RealtimeGITechnique = lightingSettings?.RealtimeGI ?? AAAARealtimeGITechnique.Off;

            if (cameraData.RealtimeGITechnique == AAAARealtimeGITechnique.LightPropagationVolumes &&
                cameraData.VolumeStack.GetComponent<AAAALPVVolumeComponent>() is { Enabled: { value: false } })
            {
                cameraData.RealtimeGITechnique = AAAARealtimeGITechnique.Off;
            }

            cameraData.SupportsProbeVolumes =
                renderingData.PipelineAsset.LightingSettings.LightProbes == AAAALightingSettings.LightProbeSystem.AdaptiveProbeVolumes;

            return cameraData;
        }

        private static AAAARendererListData CreateRendererListData(ContextContainer frameData)
        {
            AAAARendererListData rendererListData = frameData.GetOrCreate<AAAARendererListData>();

            return rendererListData;
        }

        private static AAAALightingData CreateLightingData(ContextContainer frameData)
        {
            AAAALightingData lightData = frameData.GetOrCreate<AAAALightingData>();

            return lightData;
        }

        private AAAAShadowsData CreateShadowsData(ContextContainer frameData)
        {
            AAAAShadowsData shadowsData = frameData.GetOrCreate<AAAAShadowsData>();

            return shadowsData;
        }

        private static AAAAImageBasedLightingData CreateImageBasedLightingData(ContextContainer frameData)
        {
            AAAAImageBasedLightingData imageBasedLightingData = frameData.GetOrCreate<AAAAImageBasedLightingData>();

            return imageBasedLightingData;
        }

        private static void UpdateVolumeFramework(Camera camera)
        {
            VolumeManager.instance.ResetMainStack();
            VolumeManager.instance.Update(camera.transform, 1);
        }
    }

}