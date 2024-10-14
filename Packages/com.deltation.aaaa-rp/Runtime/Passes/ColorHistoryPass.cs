using DELTation.AAAARP.FrameData;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes
{
    public class ColorHistoryPass : AAAARenderPass<ColorHistoryPass.PassData>
    {
        public ColorHistoryPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent) { }

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();

            passData.Source = builder.ReadTexture(resourceData.CameraScaledColorBuffer);
            passData.Destination = builder.WriteTexture(resourceData.CameraScaledColorHistoryBuffer);
            resourceData.CameraColorHistoryIsValid = true;
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            context.cmd.CopyTexture(data.Source, data.Destination);
        }

        public class PassData : PassDataBase
        {
            public TextureHandle Destination;
            public TextureHandle Source;
        }
    }
}