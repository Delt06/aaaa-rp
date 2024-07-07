using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.FrameData
{
    public class AAAARenderingData : ContextItem
    {
        public RenderGraph RenderGraph;
        
        public override void Reset()
        {
            RenderGraph = default;
        }
    }
}