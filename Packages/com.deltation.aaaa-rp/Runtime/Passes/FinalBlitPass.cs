using DELTation.AAAARP.FrameData;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes
{
    public class FinalBlitPassData : PassDataBase
    {
        public AAAACameraData CameraData;
        public TextureHandle Destination;
        public TextureHandle Source;
    }
    
    public class FinalBlitPass : AAAARasterRenderPass<FinalBlitPassData>
    {
        public FinalBlitPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent) { }
        
        public override string Name => "FinalBlit";
        
        protected override void Setup(IRasterRenderGraphBuilder builder, FinalBlitPassData passData, ContextContainer frameData)
        {
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            passData.CameraData = frameData.Get<AAAACameraData>();
            passData.Source = resourceData.CameraColorBuffer;
            passData.Destination = resourceData.CameraResolveColorBuffer;
            
            builder.UseTexture(passData.Source, AccessFlags.Read);
            builder.SetRenderAttachment(passData.Destination, 0, AccessFlags.Write);
        }
        
        protected override void Render(FinalBlitPassData data, RasterGraphContext context)
        {
            var source = (RTHandle) data.Source;
            var destination = (RTHandle) data.Destination;
            
            Vector4 scaleBias = AAAARenderingUtils.GetFinalBlitScaleBias(source, destination, data.CameraData);
            
            if (destination.nameID == BuiltinRenderTextureType.CameraTarget || data.CameraData.TargetTexture != null)
            {
                context.cmd.SetViewport(data.CameraData.PixelRect);
            }
            
            const float mipLevel = 0;
            const bool bilinear = true;
            Blitter.BlitTexture(context.cmd, data.Source, scaleBias, mipLevel, bilinear);
        }
    }
}