using DELTation.AAAARP.Data;
using DELTation.AAAARP.Renderers;
using DELTation.AAAARP.Utils;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.FrameData
{
    public class AAAARenderingData : ContextItem
    {
        public CullingResults CullingResults;
        public AAAARenderPipelineAsset PipelineAsset;
        public AAAARendererContainer RendererContainer;
        public RenderGraph RenderGraph;
        public AAAARenderTexturePoolSet RtPoolSet;

        public override void Reset()
        {
            PipelineAsset = default;
            RenderGraph = default;
            CullingResults = default;
            RendererContainer = default;
            RtPoolSet = default;
        }
    }
}