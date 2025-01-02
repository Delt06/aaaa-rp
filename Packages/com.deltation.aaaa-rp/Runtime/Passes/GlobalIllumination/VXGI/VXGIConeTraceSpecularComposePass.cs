using System;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.GlobalIllumination.VXGI
{
    public class VXGIConeTraceSpecularComposePass : AAAARasterRenderPass<VXGIConeTraceSpecularComposePass.PassData>, IDisposable
    {
        private const int PassIndex = 2;

        private readonly Material _material;

        public VXGIConeTraceSpecularComposePass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent)
        {
            AAAAVxgiRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAAVxgiRuntimeShaders>();
            _material = CoreUtils.CreateEngineMaterial(shaders.ConeTracePS);
        }

        public override string Name => "VXGI.ConeTrace.Specular.Compose";

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
        }

        protected override void Setup(IRasterRenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAALightingData lightingData = frameData.Get<AAAALightingData>();
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();

            passData.ScaleBias = new Vector4(1, 1, 0, 0);

            AAAAVoxelGlobalIlluminationData vxgiData = frameData.Get<AAAAVoxelGlobalIlluminationData>();
            builder.UseTexture(vxgiData.IndirectSpecularTexture, AccessFlags.Read);

            builder.SetRenderAttachment(lightingData.DeferredReflections, 0, AccessFlags.ReadWrite);
            builder.SetRenderAttachmentDepth(resourceData.CameraScaledDepthBuffer, AccessFlags.Read);
        }

        protected override void Render(PassData data, RasterGraphContext context)
        {
            Blitter.BlitTexture(context.cmd, data.ScaleBias, _material, PassIndex);
        }

        public class PassData : PassDataBase
        {
            public Vector4 ScaleBias;
        }
    }
}