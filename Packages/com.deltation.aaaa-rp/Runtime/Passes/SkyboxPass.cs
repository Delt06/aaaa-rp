using DELTation.AAAARP.FrameData;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes
{
    public class SkyboxPassData : PassDataBase
    {
        public RendererListHandle RendererList;
    }
    
    public class SkyboxPass : AAAARasterRenderPass<SkyboxPassData>
    {
        public SkyboxPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent) { }
        
        public override string Name => "Skybox";
        
        protected override void Setup(IRasterRenderGraphBuilder builder, SkyboxPassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            RendererListHandle rendererList = renderingData.RenderGraph.CreateSkyboxRendererList(cameraData.Camera);
            passData.RendererList = rendererList;
            
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            builder.UseRendererList(rendererList);
            builder.SetRenderAttachment(resourceData.CameraColorBuffer, 0, AccessFlags.ReadWrite);
            builder.SetRenderAttachmentDepth(resourceData.CameraDepthBuffer, AccessFlags.ReadWrite);
        }
        
        protected override void Render(SkyboxPassData data, RasterGraphContext context)
        {
            context.cmd.DrawRendererList(data.RendererList);
        }
    }
}