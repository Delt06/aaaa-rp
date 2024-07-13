using DELTation.AAAARP.Data;
using DELTation.AAAARP.FrameData;
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

        private readonly AAAARenderPipelineAsset _pipelineAsset;
        private readonly AAAARendererBase _renderer;
        private RenderGraph _renderGraph;

        public AAAARenderPipeline(AAAARenderPipelineAsset pipelineAsset)
        {
            _pipelineAsset = pipelineAsset;
            _renderer = new AAAARenderer();
            _renderGraph = new RenderGraph("AAAARPRenderGraph");

            AAAARenderPipelineRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAARenderPipelineRuntimeShaders>();
            Blitter.Initialize(shaders.CoreBlitPS, shaders.CoreBlitColorAndDepthPS);

            RTHandles.Initialize(Screen.width, Screen.height);
            ShaderGlobalKeywords.InitializeShaderGlobalKeywords();
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            foreach (Camera camera in cameras)
            {
                RenderSingleCamera(context, _renderer, camera);
            }

            _renderGraph.EndFrame();
        }

        private void RenderSingleCamera(ScriptableRenderContext context, AAAARendererBase renderer, Camera camera)
        {
            ContextContainer frameData = renderer.FrameData;
            CreateRenderingData(frameData);
            AAAACameraData cameraData = CreateCameraData(frameData, camera, renderer);
            CreateResourceData(frameData, cameraData);
            CreateRendererListData(frameData);

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

            using (new ProfilingScope(AAAAProfiling.Pipeline.Context.Submit))
            {
                context.Submit();
            }
        }

        protected override void Dispose(bool disposing)
        {
            Blitter.Cleanup();

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

            renderingData.RenderGraph = _renderGraph;

            return renderingData;
        }

        private static AAAACameraData CreateCameraData(ContextContainer frameData, Camera camera, AAAARendererBase renderer)
        {
            AAAACameraData cameraData = frameData.GetOrCreate<AAAACameraData>();

            cameraData.Renderer = renderer;
            cameraData.Camera = camera;
            cameraData.IsHdrEnabled = camera.allowHDR;
            cameraData.CameraType = camera.cameraType;

            Rect cameraRect = camera.rect;
            cameraData.PixelRect = camera.pixelRect;
            cameraData.PixelWidth = camera.pixelWidth;
            cameraData.PixelHeight = camera.pixelHeight;
            cameraData.RenderScale = 1.0f;
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

            return cameraData;
        }

        private static AAAARendererListData CreateRendererListData(ContextContainer frameData)
        {
            AAAARendererListData rendererListData = frameData.GetOrCreate<AAAARendererListData>();

            return rendererListData;
        }
    }
}