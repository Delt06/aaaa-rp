using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.FrameData
{
    public class AAAARenderingData : ContextItem
    {
        public RenderGraph RenderGraph;
        public CullingResults CullingResults;
        
        public override void Reset()
        {
            RenderGraph = default;
            CullingResults = default;
        }
    }
}