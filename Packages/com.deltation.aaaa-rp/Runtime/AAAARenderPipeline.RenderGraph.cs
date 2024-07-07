using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP
{
    public sealed partial class AAAARenderPipeline
    {
        private static void RecordRenderGraph(RenderGraph renderGraph, ScriptableRenderContext context, AAAARenderer renderer)
        {
            renderer.RecordRenderGraph(renderGraph, context);
        }
        
        private static void RecordAndExecuteRenderGraph(RenderGraph renderGraph, ScriptableRenderContext context, AAAARenderer renderer,
            CommandBuffer cmd, Camera camera, string cameraName)
        {
            var rgParams = new RenderGraphParameters
            {
                executionName = cameraName,
                commandBuffer = cmd,
                scriptableRenderContext = context,
                currentFrameIndex = Time.frameCount,
            };
            
            renderGraph.BeginRecording(rgParams);
            RecordRenderGraph(renderGraph, context, renderer);
            renderGraph.EndRecordingAndExecute();
        }
    }
}