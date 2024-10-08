using DELTation.AAAARP.Data;
using DELTation.AAAARP.Debugging;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Lighting;
using DELTation.AAAARP.Renderers;
using DELTation.AAAARP.Utils;
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
        private static ShadowMapPool _shadowMapPool;
        private readonly BindlessTextureContainer _bindlessTextureContainer;
        private readonly DebugDisplaySettingsUI _debugDisplaySettingsUI = new();

        private readonly AAAARenderPipelineAsset _pipelineAsset;
        private readonly AAAARendererBase _renderer;
        private readonly AAAARendererContainer _rendererContainer;
        private RenderGraph _renderGraph;

        public AAAARenderPipeline(AAAARenderPipelineAsset pipelineAsset)
        {
            _pipelineAsset = pipelineAsset;
            _renderer = new AAAARenderer();
            _renderGraph = new RenderGraph("AAAARPRenderGraph");

            AAAARenderPipelineRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAARenderPipelineRuntimeShaders>();
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

            _bindlessTextureContainer = new BindlessTextureContainer();
            _shadowMapPool = new ShadowMapPool(_bindlessTextureContainer);
            _rendererContainer = new AAAARendererContainer(_bindlessTextureContainer, pipelineAsset.MeshLODSettings, pipelineDebugDisplaySettings);
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            _shadowMapPool.Reset();
            _rendererContainer.PreRender(context);

            foreach (Camera camera in cameras)
            {
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

            _rendererContainer.Dispose();
            _shadowMapPool.Dispose();
            _bindlessTextureContainer.Dispose();

            Blitter.Cleanup();
            ConstantBuffer.ReleaseAll();

            base.Dispose(disposing);

            _renderer?.Dispose();
            _renderGraph.Cleanup();
            _renderGraph = null;
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

            return renderingData;
        }

        private static AAAACameraData CreateCameraData(ContextContainer frameData, Camera camera, AAAARendererBase renderer, AAAARenderingData renderingData)
        {
            AAAACameraData cameraData = frameData.GetOrCreate<AAAACameraData>();
            AAAAImageQualitySettings imageQualitySettings = camera.cameraType == CameraType.Game ? renderingData.PipelineAsset.ImageQualitySettings : null;
            AAAAPostProcessingSettings postProcessingSettings =
                camera.cameraType == CameraType.Game ? renderingData.PipelineAsset.PostProcessingSettings : null;
            AAAALightingSettings lightingSettings = camera.cameraType is CameraType.Game or CameraType.SceneView ? renderingData.PipelineAsset.LightingSettings : null;

            cameraData.Renderer = renderer;
            cameraData.Camera = camera;
            cameraData.IsHdrEnabled = camera.allowHDR;
            cameraData.CameraType = camera.cameraType;

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
            
            cameraData.AntiAliasingTechnique = imageQualitySettings?.AntiAliasing ?? AAAAAntiAliasingTechnique.Off;
            cameraData.UpscalingTechnique = imageQualitySettings?.Upscaling ?? AAAAUpscalingTechnique.Off;
            if (cameraData.RenderScale >= 1.0f)
            {
                cameraData.UpscalingTechnique = AAAAUpscalingTechnique.Off;
            }
            cameraData.FSRShaprness = imageQualitySettings?.FSRSharpness ?? 0.0f;
            cameraData.PostProcessingEnabled = postProcessingSettings?.AnyEnabled() ?? false;
            cameraData.AmbientOcclusionTechnique = lightingSettings?.AmbientOcclusion ?? AAAAAmbientOcclusionTechnique.Off;

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

        private static AAAAShadowsData CreateShadowsData(ContextContainer frameData)
        {
            AAAAShadowsData shadowsData = frameData.GetOrCreate<AAAAShadowsData>();

            shadowsData.ShadowMapPool = _shadowMapPool;

            return shadowsData;
        }

        private static AAAAImageBasedLightingData CreateImageBasedLightingData(ContextContainer frameData)
        {
            AAAAImageBasedLightingData imageBasedLightingData = frameData.GetOrCreate<AAAAImageBasedLightingData>();

            return imageBasedLightingData;
        }
    }
}