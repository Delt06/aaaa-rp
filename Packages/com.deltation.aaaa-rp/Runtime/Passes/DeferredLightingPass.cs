using System;
using DELTation.AAAARP.FrameData;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes
{
    public sealed class DeferredLightingPass : AAAARasterRenderPass<DeferredLightingPass.PassData>, IDisposable
    {
        private readonly Material _material;

        public DeferredLightingPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders shaders) : base(renderPassEvent) =>
            _material = CoreUtils.CreateEngineMaterial(shaders.DeferredLightingPS);

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
        }

        protected override void Setup(IRasterRenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();

            builder.UseTexture(resourceData.GBufferAlbedo, AccessFlags.Read);
            builder.UseTexture(resourceData.GBufferNormals, AccessFlags.Read);
            builder.UseTexture(resourceData.GBufferMasks, AccessFlags.Read);
            builder.UseTexture(resourceData.CameraScaledDepthBuffer, AccessFlags.Read);

            builder.SetRenderAttachment(resourceData.CameraScaledColorBuffer, 0, AccessFlags.WriteAll);
        }

        protected override void Render(PassData data, RasterGraphContext context)
        {
            var scaleBias = new Vector4(1, 1, 0, 0);
            const int pass = 0;
            Blitter.BlitTexture(context.cmd, scaleBias, _material, pass);
        }

        public class PassData : PassDataBase { }
    }
}