using DELTation.AAAARP.FrameData;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.Lighting
{
    public class DeferredReflectionsSetupPass : AAAARasterRenderPass<DeferredReflectionsSetupPass.PassData>
    {
        private readonly Material _material;

        public DeferredReflectionsSetupPass(AAAARenderPassEvent renderPassEvent, Material material) : base(renderPassEvent) => _material = material;

        protected override void Setup(IRasterRenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAALightingData lightingData = frameData.Get<AAAALightingData>();
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();

            TextureDesc textureDesc = resourceData.CameraScaledColorDesc;
            textureDesc.name = nameof(AAAALightingData.DeferredReflections);
            textureDesc.clearBuffer = true;
            textureDesc.clearColor = Color.black;
            lightingData.DeferredReflections = renderingData.RenderGraph.CreateTexture(textureDesc);

            passData.ApplyEnvironment = lightingData.AmbientIntensity > 0.0f;

            builder.SetRenderAttachment(lightingData.DeferredReflections, 0, AccessFlags.ReadWrite);
            builder.SetRenderAttachmentDepth(resourceData.CameraScaledDepthBuffer, AccessFlags.Read);
        }

        protected override void Render(PassData data, RasterGraphContext context)
        {
            if (data.ApplyEnvironment)
            {
                var scaleBias = new Vector4(1, 1, 0, 0);
                const int environmentPass = 0;
                Blitter.BlitTexture(context.cmd, scaleBias, _material, environmentPass);
            }
        }

        public class PassData : PassDataBase
        {
            public bool ApplyEnvironment;
        }
    }
}