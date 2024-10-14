using DELTation.AAAARP.FrameData;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.GlobalIllumination.SSR
{
    public class SSRComposePass : AAAARasterRenderPass<SSRComposePass.PassData>
    {
        private const int ComposePass = 2;
        private readonly Material _material;

        public SSRComposePass(AAAARenderPassEvent renderPassEvent, Material material) : base(renderPassEvent) => _material = material;

        public override string Name => "SSR.Compose";

        protected override void Setup(IRasterRenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAALightingData lightingData = frameData.Get<AAAALightingData>();
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();

            passData.ResolveResult = lightingData.SSRResolveResult;
            builder.UseTexture(passData.ResolveResult, AccessFlags.Read);

            builder.SetRenderAttachment(lightingData.DeferredReflections, 0, AccessFlags.ReadWrite);
            builder.SetRenderAttachmentDepth(resourceData.CameraScaledDepthBuffer, AccessFlags.Read);
        }

        protected override void Render(PassData data, RasterGraphContext context)
        {
            var scaleBias = new Vector4(1, 1, 0, 0);
            Blitter.BlitTexture(context.cmd, data.ResolveResult, scaleBias, _material, ComposePass);
        }

        public class PassData : PassDataBase
        {
            public TextureHandle ResolveResult;
        }
    }
}