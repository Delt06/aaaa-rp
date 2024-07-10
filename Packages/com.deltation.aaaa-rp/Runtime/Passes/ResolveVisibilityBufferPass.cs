using DELTation.AAAARP.FrameData;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes
{
    public class ResolveVisibilityBufferPassData : PassDataBase
    {
        public TextureHandle VisibilityBuffer;
    }
    
    public class ResolveVisibilityBufferPass : AAAARasterRenderPass<ResolveVisibilityBufferPassData>
    {
        private readonly Material _resolveMaterial;
        
        public ResolveVisibilityBufferPass(AAAARenderPassEvent renderPassEvent, Material resolveMaterial) : base(renderPassEvent) =>
            _resolveMaterial = resolveMaterial;
        
        public override string Name => "ResolveVisibilityBuffer";
        
        protected override void Setup(IRasterRenderGraphBuilder builder, ResolveVisibilityBufferPassData passData, ContextContainer frameData)
        {
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            passData.VisibilityBuffer = resourceData.VisibilityBuffer;
            
            builder.AllowGlobalStateModification(true);
            builder.UseTexture(passData.VisibilityBuffer, AccessFlags.Read);
            builder.SetRenderAttachment(resourceData.GBufferAlbedo, 0, AccessFlags.ReadWrite);
            builder.SetRenderAttachment(resourceData.GBufferNormals, 1, AccessFlags.ReadWrite);
            builder.SetRenderAttachmentDepth(resourceData.CameraDepthBuffer, AccessFlags.Read);
        }
        
        protected override void Render(ResolveVisibilityBufferPassData data, RasterGraphContext context)
        {
            context.cmd.SetGlobalTexture(AAAAResourceData.ShaderPropertyID._VisibilityBuffer, data.VisibilityBuffer);
            
            var scaleBias = new Vector4(1, 1, 0, 0);
            const int pass = 0;
            Blitter.BlitTexture(context.cmd, scaleBias, _resolveMaterial, pass);
        }
    }
}