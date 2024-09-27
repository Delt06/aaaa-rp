using DELTation.AAAARP.Data;
using DELTation.AAAARP.Renderers;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.FrameData
{
    public class AAAARenderingData : ContextItem
    {
        public AAAARenderPipelineAsset PipelineAsset;
        public CullingResults CullingResults;
        public RenderGraph RenderGraph;
        public AAAARendererContainer RendererContainer;

        public override void Reset()
        {
            PipelineAsset = default;
            RenderGraph = default;
            CullingResults = default;
            RendererContainer = default;
        }
    }
}