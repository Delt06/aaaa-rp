using System;
using DELTation.AAAARP.Data;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using static DELTation.AAAARP.Lighting.AAAAVxgiCommon;

namespace DELTation.AAAARP.Passes.GlobalIllumination.VXGI
{
    public class VXGIConeTraceDiffusePass : AAAARasterRenderPass<VXGIConeTraceDiffusePass.PassData>, IDisposable
    {
        private const int PassIndex = 0;
        private readonly Material _gatherMaterial;
        private readonly Material _material;

        public VXGIConeTraceDiffusePass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent)
        {
            AAAAVxgiRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAAVxgiRuntimeShaders>();
            _material = CoreUtils.CreateEngineMaterial(shaders.ConeTracePS);
            _gatherMaterial = CoreUtils.CreateEngineMaterial(shaders.ConeTracePS);
            _gatherMaterial.EnableKeyword("GATHER");
        }

        public override string Name => "VXGI.ConeTrace.Diffuse";

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
            CoreUtils.Destroy(_gatherMaterial);
        }

        protected override void Setup(IRasterRenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAAVoxelGlobalIlluminationData vxgiData = frameData.Get<AAAAVoxelGlobalIlluminationData>();

            passData.ScaleBias = new Vector4(1, 1, 0, 0);
            passData.Material = vxgiData.RenderScale > 1 ? _gatherMaterial : _material;

            if (cameraData.AmbientOcclusionTechnique == AAAAAmbientOcclusionTechnique.XeGTAO)
            {
                AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
                if (renderingData.PipelineAsset.LightingSettings.GTAOSettings.BentNormals)
                {
                    AAAALightingData lightingData = frameData.Get<AAAALightingData>();
                    builder.UseTexture(lightingData.GTAOTerm);
                }
            }

            builder.SetRenderAttachment(vxgiData.IndirectDiffuseTexture, 0, AccessFlags.Write);
            builder.SetGlobalTextureAfterPass(vxgiData.IndirectDiffuseTexture, GlobalShaderIDs._VXGIIndirectDiffuseTexture);
        }

        protected override void Render(PassData data, RasterGraphContext context)
        {
            Blitter.BlitTexture(context.cmd, data.ScaleBias, data.Material, PassIndex);
        }

        public class PassData : PassDataBase
        {
            public Material Material;
            public Vector4 ScaleBias;
        }
    }
}