using DELTation.AAAARP.FrameData;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.Debugging
{
    public class DebugSetupPass : AAAARenderPass<PassDataBase>
    {
        public DebugSetupPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent) { }

        public override string Name => "Debug.Setup";

        protected override void Setup(RenderGraphBuilder builder, PassDataBase passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAADebugData debugData = frameData.GetOrCreate<AAAADebugData>();
            debugData.Init(renderingData.RenderGraph);
        }

        protected override void Render(PassDataBase data, RenderGraphContext context) { }
    }
}