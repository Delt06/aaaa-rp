using DELTation.AAAARP.FrameData;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes
{
    public sealed class SkyboxPass : AAAARasterRenderPass<SkyboxPass.PassData>
    {
        public SkyboxPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent) { }

        protected override void Setup(IRasterRenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            RendererListHandle rendererList = renderingData.RenderGraph.CreateSkyboxRendererList(cameraData.Camera);
            passData.RendererList = rendererList;

            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            builder.UseRendererList(rendererList);
            builder.SetRenderAttachment(resourceData.CameraScaledColorBuffer, 0, AccessFlags.ReadWrite);
            builder.SetRenderAttachmentDepth(resourceData.CameraScaledDepthBuffer, AccessFlags.ReadWrite);
        }

        protected override void Render(PassData data, RasterGraphContext context)
        {
            context.cmd.DrawRendererList(data.RendererList);
        }

        public class PassData : PassDataBase
        {
            public RendererListHandle RendererList;
        }
    }
}