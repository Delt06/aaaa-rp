using DELTation.AAAARP.FrameData;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes
{
    public class ResolveVisibilityBufferPassData : PassDataBase
    {
        public TextureHandle Destination;
        public TextureHandle Source;
    }
    
    public class ResolveVisibilityBufferPass : AAAARasterRenderPass<ResolveVisibilityBufferPassData>
    {
        private readonly Material _visibilityBufferPreviewMaterial;
        
        public ResolveVisibilityBufferPass(AAAARenderPassEvent renderPassEvent, Material visibilityBufferPreviewMaterial) : base(renderPassEvent) =>
            _visibilityBufferPreviewMaterial = visibilityBufferPreviewMaterial;
        
        public override string Name => "ResolveVisibilityBuffer";
        
        protected override void Setup(IRasterRenderGraphBuilder builder, ResolveVisibilityBufferPassData passData, ContextContainer frameData)
        {
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            passData.Source = resourceData.VisibilityBuffer;
            passData.Destination = resourceData.CameraColorBuffer;
            
            builder.UseTexture(passData.Source, AccessFlags.Read);
            builder.SetRenderAttachment(passData.Destination, 0, AccessFlags.ReadWrite);
        }
        
        protected override void Render(ResolveVisibilityBufferPassData data, RasterGraphContext context)
        {
            var scaleBias = new Vector4(1, 1, 0, 0);
            const int pass = 0;
            Blitter.BlitTexture(context.cmd, data.Source, scaleBias, _visibilityBufferPreviewMaterial, pass);
        }
    }
}