using DELTation.AAAARP.FrameData;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.PostProcessing
{
    public sealed class BilinearUpscalePass : AAAARasterRenderPass<BilinearUpscalePass.PassData>
    {
        public BilinearUpscalePass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent) { }

        protected override void Setup(IRasterRenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            passData.Source = resourceData.CameraScaledColorBuffer;
            passData.Destination = resourceData.CameraColorBuffer;

            builder.UseTexture(passData.Source, AccessFlags.Read);
            builder.SetRenderAttachment(passData.Destination, 0, AccessFlags.Write);
        }

        protected override void Render(PassData data, RasterGraphContext context)
        {
            const float mipLevel = 0;
            const bool bilinear = true;
            Blitter.BlitTexture(context.cmd, data.Source, new Vector4(1, 1, 0, 0), mipLevel, bilinear);
        }

        public class PassData : PassDataBase
        {
            public TextureHandle Destination;
            public TextureHandle Source;
        }
    }
}