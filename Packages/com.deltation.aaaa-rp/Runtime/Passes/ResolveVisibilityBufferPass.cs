using System;
using DELTation.AAAARP.FrameData;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes
{
    public class ResolveVisibilityBufferPass : AAAARasterRenderPass<ResolveVisibilityBufferPass.PassData>, IDisposable
    {
        private readonly Material _material;

        public ResolveVisibilityBufferPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders shaders) : base(renderPassEvent) =>
            _material = CoreUtils.CreateEngineMaterial(shaders.VisibilityBufferResolvePS);

        public override string Name => "ResolveVisibilityBuffer";

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
        }

        protected override void Setup(IRasterRenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();

            builder.UseTexture(resourceData.VisibilityBuffer, AccessFlags.Read);
            builder.SetRenderAttachment(resourceData.GBufferAlbedo, 0, AccessFlags.ReadWrite);
            builder.SetRenderAttachment(resourceData.GBufferNormals, 1, AccessFlags.ReadWrite);
            builder.SetRenderAttachment(resourceData.GBufferMasks, 2, AccessFlags.ReadWrite);
            builder.SetRenderAttachmentDepth(resourceData.CameraDepthBuffer, AccessFlags.Read);

            builder.SetGlobalTextureAfterPass(resourceData.GBufferAlbedo, AAAAResourceData.ShaderPropertyID._GBuffer_Albedo);
            builder.SetGlobalTextureAfterPass(resourceData.GBufferNormals, AAAAResourceData.ShaderPropertyID._GBuffer_Normals);
            builder.SetGlobalTextureAfterPass(resourceData.GBufferMasks, AAAAResourceData.ShaderPropertyID._GBuffer_Masks);
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