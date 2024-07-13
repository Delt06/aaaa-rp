using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.FrameData
{
    public class AAAARenderingData : ContextItem
    {
        public CullingResults CullingResults;
        public RenderGraph RenderGraph;

        public override void Reset()
        {
            RenderGraph = default;
            CullingResults = default;
        }
    }
}