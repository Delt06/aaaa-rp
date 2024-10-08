using System;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.IBL
{
    public class BRDFIntegrationPass : AAAARenderPass<BRDFIntegrationPass.PassData>, IDisposable
    {
        private readonly Material _material;

        public BRDFIntegrationPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders runtimeShaders) : base(renderPassEvent) =>
            _material = CoreUtils.CreateEngineMaterial(runtimeShaders.BRDFIntegrationPS);

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
        }

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAAImageBasedLightingData imageBasedLightingData = frameData.Get<AAAAImageBasedLightingData>();

            if (!imageBasedLightingData.BRDFLutIsDirty)
            {
                passData.CullPass = true;
                return;
            }

            passData.CullPass = false;
            imageBasedLightingData.BRDFLutIsDirty = false;

            passData.Material = _material;
            passData.Destination = builder.WriteTexture(imageBasedLightingData.BRDFLut);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            if (data.CullPass)
            {
                return;
            }

            context.cmd.SetRenderTarget(data.Destination);

            Material material = data.Material;
            const int shaderPass = 0;
            Blitter.BlitTexture(context.cmd, new Vector4(1, 1, 0, 0), material, shaderPass);
        }

        public class PassData : PassDataBase
        {
            public bool CullPass;
            public TextureHandle Destination;
            public Material Material;
        }
    }
}