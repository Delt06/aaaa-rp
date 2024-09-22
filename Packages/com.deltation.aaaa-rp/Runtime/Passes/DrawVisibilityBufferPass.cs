using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Renderers;
using UnityEngine;
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
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            AAAARendererListData rendererListData = frameData.Get<AAAARendererListData>();
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();

            passData.CameraType = cameraData.CameraType;

            passData.RendererListHandle = rendererListData.VisibilityBuffer.Handle;
            passData.RendererContainer = renderingData.RendererContainer;

            builder.UseRendererList(passData.RendererListHandle);
            builder.SetRenderAttachment(resourceData.VisibilityBuffer, 0, AccessFlags.ReadWrite);
            builder.SetRenderAttachmentDepth(resourceData.CameraScaledDepthBuffer, AccessFlags.ReadWrite);

            builder.SetGlobalTextureAfterPass(resourceData.VisibilityBuffer, AAAAResourceData.ShaderPropertyID._VisibilityBuffer);
        }

        protected override void Render(PassData data, RasterGraphContext context)
        {
            data.RendererContainer.Draw(data.CameraType, context.cmd);
            context.cmd.DrawRendererList(data.RendererListHandle);
        }

        public class PassData : PassDataBase
        {
            public CameraType CameraType;
            public AAAARendererContainer RendererContainer;
            public RendererListHandle RendererListHandle;
        }
    }
}