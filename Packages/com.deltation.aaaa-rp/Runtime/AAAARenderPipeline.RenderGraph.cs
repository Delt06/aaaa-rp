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
            var renderGraphParameters = new RenderGraphParameters
            {
                executionName = cameraName,
                commandBuffer = cmd,
                scriptableRenderContext = context,
                currentFrameIndex = Time.frameCount,
            };
            
            ContextContainer frameData = renderer.FrameData;
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            
            renderGraph.BeginRecording(renderGraphParameters);
            {
                renderer.ImportBackbuffer(cameraData);
                resourceData.InitTextures(renderGraph, cameraData);
                
                resourceData.BeginFrame();
                renderer.BeginFrame(renderGraph, context);
                RecordRenderGraph(renderGraph, context, renderer);
                renderer.EndFrame();
                resourceData.EndFrame();
            }
            renderGraph.EndRecordingAndExecute();
        }
    }
}