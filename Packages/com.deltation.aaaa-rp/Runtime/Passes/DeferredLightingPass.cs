using DELTation.AAAARP.FrameData;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes
{
    public class DeferredLightingPassData : PassDataBase
    {
        public TextureHandle Source;
    }
    
    public class DeferredLightingPass : AAAARasterRenderPass<DeferredLightingPassData>
    {
        public DeferredLightingPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent) { }
        
        public override string Name => "DeferredLighting";
        
        protected override void Setup(IRasterRenderGraphBuilder builder, DeferredLightingPassData passData, ContextContainer frameData)
        {
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            passData.Source = resourceData.GBufferAlbedo;
            
            builder.UseTexture(resourceData.GBufferAlbedo, AccessFlags.Read);
            builder.UseTexture(resourceData.GBufferNormals, AccessFlags.Read);
            builder.SetRenderAttachment(resourceData.CameraColorBuffer, 0, AccessFlags.WriteAll);
        }
        
        protected override void Render(DeferredLightingPassData data, RasterGraphContext context)
        {
            var scaleBias = new Vector4(1, 1, 0, 0);
            Blitter.BlitTexture(context.cmd, data.Source, scaleBias, 0, false);
        }
    }
}