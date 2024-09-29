using DELTation.AAAARP.FrameData;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP
{
    public sealed partial class AAAARenderPipeline
    {
        private static void RecordRenderGraph(RenderGraph renderGraph, ScriptableRenderContext context, AAAARendererBase renderer)
        {
            renderer.RecordRenderGraph(renderGraph, context);
        }

        private static void RecordAndExecuteRenderGraph(RenderGraph renderGraph, ScriptableRenderContext context, AAAARendererBase renderer,
            CommandBuffer cmd, string cameraName)
        {
            ContextContainer frameData = renderer.FrameData;
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();

            const bool stereoAware = false;
            if (!cameraData.Camera.TryGetCullingParameters(stereoAware, out ScriptableCullingParameters cullingParameters))
            {
                return;
            }

            var renderGraphParameters = new RenderGraphParameters
            {
                executionName = cameraName,
                commandBuffer = cmd,
                scriptableRenderContext = context,
                currentFrameIndex = Time.frameCount,
            };

            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            AAAARendererListData rendererListData = frameData.Get<AAAARendererListData>();

            AAAAImageBasedLightingData imageBasedLightingData = frameData.Get<AAAAImageBasedLightingData>();
            imageBasedLightingData.Init(renderingData.PipelineAsset.ImageBasedLightingSettings, renderGraph);

            renderGraph.BeginRecording(renderGraphParameters);
            {
                renderingData.CullingResults = context.Cull(ref cullingParameters);

                renderer.ImportBackbuffer(cameraData);
                resourceData.InitTextures(renderGraph, renderingData, cameraData);
                rendererListData.Init(renderingData, cameraData);

                resourceData.BeginFrame();
                renderer.BeginFrame(renderGraph, context, frameData);
                RecordRenderGraph(renderGraph, context, renderer);
                renderer.EndFrame();
                resourceData.EndFrame();
            }
            renderGraph.EndRecordingAndExecute();
        }
    }
}