using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using static DELTation.AAAARP.Lighting.AAAALpvCommon;

namespace DELTation.AAAARP.Passes.GlobalIllumination.LPV
{
    public class LPVResolvePass : AAAARenderPass<LPVResolvePass.PassData>
    {
        private const int KernelIndex = 0;
        private readonly ComputeShader _computeShader;
        private readonly PassType _type;

        public LPVResolvePass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders shaders) : base(renderPassEvent) =>
            _computeShader = shaders.LpvResolveCS;

        public override string Name => "LPV.Resolve";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAALightPropagationVolumesData lpvData = frameData.GetOrCreate<AAAALightPropagationVolumesData>();

            passData.BlockingPotential = lpvData.BlockingPotential;

            TextureDesc destinationSHDesc = lpvData.UnpackedGridTextures.SHDesc;

            const string namePrefix = "LPVGrid_";

            lpvData.UnpackedGridTextures.RedSH = renderingData.RenderGraph.CreateTexture(new TextureDesc(destinationSHDesc)
                {
                    name = namePrefix + nameof(AAAALightPropagationVolumesData.GridTextureSet.RedSH),
                }
            );
            lpvData.UnpackedGridTextures.GreenSH = renderingData.RenderGraph.CreateTexture(new TextureDesc(destinationSHDesc)
                {
                    name = namePrefix + nameof(AAAALightPropagationVolumesData.GridTextureSet.GreenSH),
                }
            );
            lpvData.UnpackedGridTextures.BlueSH = renderingData.RenderGraph.CreateTexture(new TextureDesc(destinationSHDesc)
                {
                    name = namePrefix + nameof(AAAALightPropagationVolumesData.GridTextureSet.BlueSH),
                }
            );
            lpvData.UnpackedGridTextures.BlockingPotentialSH = renderingData.RenderGraph.CreateTexture(new TextureDesc(destinationSHDesc)
                {
                    name = namePrefix + nameof(AAAALightPropagationVolumesData.GridTextureSet.BlockingPotentialSH),
                    format = GridBlockingPotentialFormat,
                }
            );

            passData.SourceRedSH = builder.ReadTexture(lpvData.PackedGridTextures.RedSH);
            passData.SourceGreenSH = builder.ReadTexture(lpvData.PackedGridTextures.GreenSH);
            passData.SourceBlueSH = builder.ReadTexture(lpvData.PackedGridTextures.BlueSH);
            passData.SourceBlockingPotentialSH = passData.BlockingPotential ? builder.ReadTexture(lpvData.PackedGridTextures.BlockingPotentialSH) : default;

            passData.DestinationRedSH = builder.WriteTexture(lpvData.UnpackedGridTextures.RedSH);
            passData.DestinationGreenSH = builder.WriteTexture(lpvData.UnpackedGridTextures.GreenSH);
            passData.DestinationBlueSH = builder.WriteTexture(lpvData.UnpackedGridTextures.BlueSH);
            passData.DestinationBlockingPotentialSH =
                passData.BlockingPotential ? builder.WriteTexture(lpvData.UnpackedGridTextures.BlockingPotentialSH) : default;

            uint3 threadGroupSize;
            _computeShader.GetKernelThreadGroupSizes(KernelIndex, out threadGroupSize.x, out threadGroupSize.y, out threadGroupSize.z);
            passData.ThreadGroups = AAAAMathUtils.AlignUp(lpvData.GridSize, (int3) threadGroupSize) / (int3) threadGroupSize;
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            CoreUtils.SetKeyword(context.cmd, _computeShader, ShaderKeywords.BLOCKING_POTENTIAL, data.BlockingPotential);

            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._SourceRedSH, data.SourceRedSH);
            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._SourceGreenSH, data.SourceGreenSH);
            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._SourceBlueSH, data.SourceBlueSH);
            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._DestinationRedSH, data.DestinationRedSH);
            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._DestinationGreenSH, data.DestinationGreenSH);
            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._DestinationBlueSH, data.DestinationBlueSH);

            if (data.BlockingPotential)
            {
                context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._SourceBlockingPotentialSH, data.SourceBlockingPotentialSH);
                context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._DestinationBlockingPotentialSH, data.DestinationBlockingPotentialSH);
            }

            context.cmd.DispatchCompute(_computeShader, KernelIndex, data.ThreadGroups.x, data.ThreadGroups.y, data.ThreadGroups.z);

            context.cmd.SetGlobalTexture(GlobalShaderIDs._LPVGridRedSH, data.DestinationRedSH);
            context.cmd.SetGlobalTexture(GlobalShaderIDs._LPVGridGreenSH, data.DestinationGreenSH);
            context.cmd.SetGlobalTexture(GlobalShaderIDs._LPVGridBlueSH, data.DestinationBlueSH);
        }

        public class PassData : PassDataBase
        {
            public bool BlockingPotential;
            public TextureHandle DestinationBlockingPotentialSH;
            public TextureHandle DestinationBlueSH;
            public TextureHandle DestinationGreenSH;
            public TextureHandle DestinationRedSH;
            public TextureHandle SourceBlockingPotentialSH;
            public TextureHandle SourceBlueSH;
            public TextureHandle SourceGreenSH;
            public TextureHandle SourceRedSH;
            public int3 ThreadGroups;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static readonly int _SourceRedSH = Shader.PropertyToID(nameof(_SourceRedSH));
            public static readonly int _SourceGreenSH = Shader.PropertyToID(nameof(_SourceGreenSH));
            public static readonly int _SourceBlueSH = Shader.PropertyToID(nameof(_SourceBlueSH));
            public static readonly int _SourceBlockingPotentialSH = Shader.PropertyToID(nameof(_SourceBlockingPotentialSH));
            public static readonly int _DestinationRedSH = Shader.PropertyToID(nameof(_DestinationRedSH));
            public static readonly int _DestinationGreenSH = Shader.PropertyToID(nameof(_DestinationGreenSH));
            public static readonly int _DestinationBlueSH = Shader.PropertyToID(nameof(_DestinationBlueSH));
            public static readonly int _DestinationBlockingPotentialSH = Shader.PropertyToID(nameof(_DestinationBlockingPotentialSH));
        }
    }
}