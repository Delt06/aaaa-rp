using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using static DELTation.AAAARP.Lighting.AAAALightPropagationVolumes;

namespace DELTation.AAAARP.Passes.GlobalIllumination.LPV
{
    public class LPVResolvePass : AAAARenderPass<LPVResolvePass.PassData>
    {
        private const int KernelIndex = 0;
        private readonly ComputeShader _computeShader;

        public LPVResolvePass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders shaders) : base(renderPassEvent) =>
            _computeShader = shaders.LpvResolveCS;

        public override string Name => "LPV.Resolve";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAALightPropagationVolumesData lpvData = frameData.GetOrCreate<AAAALightPropagationVolumesData>();

            var destinationSHDesc = new TextureDesc
            {
                width = lpvData.GridSize,
                height = lpvData.GridSize,
                slices = lpvData.GridSize,
                dimension = TextureDimension.Tex3D,
                format = GraphicsFormat.R32G32B32A32_SFloat,
                enableRandomWrite = true,
                filterMode = FilterMode.Trilinear,
                msaaSamples = MSAASamples.None,
                useMipMap = false,
            };

            const string namePrefix = "LPVGrid_";
            lpvData.UnpackedGridTextures = new AAAALightPropagationVolumesData.GridTextureSet
            {
                SHDesc = destinationSHDesc,
                RedSH = renderingData.RenderGraph.CreateTexture(new TextureDesc(destinationSHDesc)
                    {
                        name = namePrefix + nameof(AAAALightPropagationVolumesData.GridTextureSet.RedSH),
                    }
                ),
                GreenSH = renderingData.RenderGraph.CreateTexture(new TextureDesc(destinationSHDesc)
                    {
                        name = namePrefix + nameof(AAAALightPropagationVolumesData.GridTextureSet.GreenSH),
                    }
                ),
                BlueSH = renderingData.RenderGraph.CreateTexture(new TextureDesc(destinationSHDesc)
                    {
                        name = namePrefix + nameof(AAAALightPropagationVolumesData.GridTextureSet.BlueSH),
                    }
                ),
                BlockingPotentialSH = renderingData.RenderGraph.CreateTexture(new TextureDesc(destinationSHDesc)
                    {
                        name = namePrefix + nameof(AAAALightPropagationVolumesData.GridTextureSet.BlockingPotentialSH),
                        format = GraphicsFormat.R16G16B16A16_UNorm,
                    }
                ),
            };

            passData.SourceRedSH = builder.ReadBuffer(lpvData.PackedGridBuffers.RedSH);
            passData.SourceGreenSH = builder.ReadBuffer(lpvData.PackedGridBuffers.GreenSH);
            passData.SourceBlueSH = builder.ReadBuffer(lpvData.PackedGridBuffers.BlueSH);
            passData.SourceBlockingPotentialSH = builder.ReadBuffer(lpvData.PackedGridBuffers.BlockingPotentialSH);

            passData.DestinationRedSH = builder.WriteTexture(lpvData.UnpackedGridTextures.RedSH);
            passData.DestinationGreenSH = builder.WriteTexture(lpvData.UnpackedGridTextures.GreenSH);
            passData.DestinationBlueSH = builder.WriteTexture(lpvData.UnpackedGridTextures.BlueSH);
            passData.DestinationBlockingPotentialSH = builder.WriteTexture(lpvData.UnpackedGridTextures.BlockingPotentialSH);

            _computeShader.GetKernelThreadGroupSizes(KernelIndex, out uint threadGroupSizeX, out uint _, out uint _);
            int totalCellCount = lpvData.GridSize * lpvData.GridSize * lpvData.GridSize;
            passData.ThreadGroupsX = AAAAMathUtils.AlignUp(totalCellCount, (int) threadGroupSizeX) / (int) threadGroupSizeX;
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            context.cmd.SetComputeBufferParam(_computeShader, KernelIndex, ShaderIDs._SourceRedSH, data.SourceRedSH);
            context.cmd.SetComputeBufferParam(_computeShader, KernelIndex, ShaderIDs._SourceGreenSH, data.SourceGreenSH);
            context.cmd.SetComputeBufferParam(_computeShader, KernelIndex, ShaderIDs._SourceBlueSH, data.SourceBlueSH);
            context.cmd.SetComputeBufferParam(_computeShader, KernelIndex, ShaderIDs._SourceBlockingPotentialSH, data.SourceBlockingPotentialSH);
            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._DestinationRedSH, data.DestinationRedSH);
            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._DestinationGreenSH, data.DestinationGreenSH);
            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._DestinationBlueSH, data.DestinationBlueSH);
            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._DestinationBlockingPotentialSH, data.DestinationBlockingPotentialSH);
            context.cmd.DispatchCompute(_computeShader, KernelIndex, data.ThreadGroupsX, 1, 1);

            context.cmd.SetGlobalTexture(GlobalShaderIDs._LPVGridRedSH, data.DestinationRedSH);
            context.cmd.SetGlobalTexture(GlobalShaderIDs._LPVGridGreenSH, data.DestinationGreenSH);
            context.cmd.SetGlobalTexture(GlobalShaderIDs._LPVGridBlueSH, data.DestinationBlueSH);
        }

        public class PassData : PassDataBase
        {
            public TextureHandle DestinationBlockingPotentialSH;
            public TextureHandle DestinationBlueSH;
            public TextureHandle DestinationGreenSH;
            public TextureHandle DestinationRedSH;
            public BufferHandle SourceBlockingPotentialSH;
            public BufferHandle SourceBlueSH;
            public BufferHandle SourceGreenSH;
            public BufferHandle SourceRedSH;
            public int ThreadGroupsX;
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