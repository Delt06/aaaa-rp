using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.FrameData
{
    public class AAAARenderingData : ContextItem
    {
        public CullingResults CullingResults;
        public RenderGraph RenderGraph;
        public AAAAVisibilityBufferContainer VisibilityBufferContainer;

        public override void Reset()
        {
            RenderGraph = default;
            CullingResults = default;
            VisibilityBufferContainer = default;
        }
    }
}