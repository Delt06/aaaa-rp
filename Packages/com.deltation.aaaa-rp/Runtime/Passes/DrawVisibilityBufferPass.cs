using DELTation.AAAARP.FrameData;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes
{
    public class DrawVisibilityBufferPass : AAAARasterRenderPass<DrawVisibilityBufferPass.PassData>
    {
        public DrawVisibilityBufferPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent) { }

        public override string Name => "DrawVisibilityBuffer";

        protected override void Setup(IRasterRenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            AAAARendererListData rendererListData = frameData.Get<AAAARendererListData>();
            passData.RendererListHandle = rendererListData.VisibilityBuffer.Handle;

            builder.UseRendererList(passData.RendererListHandle);
            builder.SetRenderAttachment(resourceData.VisibilityBuffer, 0, AccessFlags.ReadWrite);
            builder.SetRenderAttachmentDepth(resourceData.CameraScaledDepthBuffer, AccessFlags.ReadWrite);

            builder.SetGlobalTextureAfterPass(resourceData.VisibilityBuffer, AAAAResourceData.ShaderPropertyID._VisibilityBuffer);
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