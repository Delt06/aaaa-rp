using DELTation.AAAARP.Passes;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP
{
    public class AAAARenderer : AAAARendererBase
    {
        private readonly BlitToCameraTargetPass _blitToCameraTargetPass;
        
        public AAAARenderer() => _blitToCameraTargetPass = new BlitToCameraTargetPass(AAAARenderPassEvent.AfterRendering);
        
        protected override void Setup(RenderGraph renderGraph, ScriptableRenderContext context)
        {
            EnqueuePass(_blitToCameraTargetPass);
        }
    }
}