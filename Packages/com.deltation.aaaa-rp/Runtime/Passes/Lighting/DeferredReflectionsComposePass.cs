using DELTation.AAAARP.FrameData;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.Lighting
{
    public class DeferredReflectionsComposePass : AAAARasterRenderPass<DeferredReflectionsComposePass.PassData>
    {
        private readonly Material _material;

        public DeferredReflectionsComposePass(AAAARenderPassEvent renderPassEvent, Material material) : base(renderPassEvent) => _material = material;

        protected override void Setup(IRasterRenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAALightingData lightingData = frameData.Get<AAAALightingData>();
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();

            passData.Source = lightingData.DeferredReflections;
            builder.UseTexture(lightingData.DeferredReflections, AccessFlags.Read);

            builder.SetRenderAttachment(resourceData.CameraScaledColorBuffer, 0, AccessFlags.ReadWrite);
            builder.SetRenderAttachmentDepth(resourceData.CameraScaledDepthBuffer, AccessFlags.Read);
        }

        protected override void Render(PassData data, RasterGraphContext context)
        {
            var scaleBias = new Vector4(1, 1, 0, 0);
            const int composePass = 1;
            Blitter.BlitTexture(context.cmd, data.Source, scaleBias, _material, composePass);
        }

        public class PassData : PassDataBase
        {
            public TextureHandle Source;
        }
    }
}