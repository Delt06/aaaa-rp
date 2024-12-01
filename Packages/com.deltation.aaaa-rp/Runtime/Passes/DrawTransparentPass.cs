using DELTation.AAAARP.FrameData;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes
{
    public sealed class DrawTransparentPass : AAAARasterRenderPass<DrawTransparentPass.PassData>
    {
        public DrawTransparentPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent) { }

        protected override void Setup(IRasterRenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            AAAARendererListData rendererListData = frameData.Get<AAAARendererListData>();

            passData.RendererListHandle = rendererListData.Transparent.Handle;

            builder.UseRendererList(passData.RendererListHandle);
            builder.SetRenderAttachment(resourceData.CameraScaledColorBuffer, 0, AccessFlags.ReadWrite);
            builder.SetRenderAttachmentDepth(resourceData.CameraScaledDepthBuffer, AccessFlags.Read);
        }

        protected override void Render(PassData data, RasterGraphContext context)
        {
            context.cmd.DrawRendererList(data.RendererListHandle);
        }

        public class PassData : PassDataBase
        {
            public RendererListHandle RendererListHandle;
        }
    }
}