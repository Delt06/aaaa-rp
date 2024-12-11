using System;
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
        public enum PassType
        {
            Radiance,
            BlockingPotential,
        }

        private const int KernelIndex = 0;
        private readonly ComputeShader _computeShader;
        private readonly PassType _type;

        public LPVResolvePass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders shaders, PassType type) : base(renderPassEvent)
        {
            _computeShader = shaders.LpvResolveCS;
            _type = type;
            Name = "LPV.Resolve." + type;
        }

        public override string Name { get; }

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAALightPropagationVolumesData lpvData = frameData.GetOrCreate<AAAALightPropagationVolumesData>();

            TextureDesc destinationSHDesc = lpvData.UnpackedGridTextures.SHDesc;

            const string namePrefix = "LPVGrid_";

            switch (_type)
            {

                case PassType.Radiance:
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

                    passData.SourceRedSH = builder.ReadBuffer(lpvData.PackedGridBuffers.RedSH);
                    passData.SourceGreenSH = builder.ReadBuffer(lpvData.PackedGridBuffers.GreenSH);
                    passData.SourceBlueSH = builder.ReadBuffer(lpvData.PackedGridBuffers.BlueSH);

                    passData.DestinationRedSH = builder.WriteTexture(lpvData.UnpackedGridTextures.RedSH);
                    passData.DestinationGreenSH = builder.WriteTexture(lpvData.UnpackedGridTextures.GreenSH);
                    passData.DestinationBlueSH = builder.WriteTexture(lpvData.UnpackedGridTextures.BlueSH);
                    break;
                case PassType.BlockingPotential:
                    lpvData.UnpackedGridTextures.BlockingPotentialSH = renderingData.RenderGraph.CreateTexture(new TextureDesc(destinationSHDesc)
                        {
                            name = namePrefix + nameof(AAAALightPropagationVolumesData.GridTextureSet.BlockingPotentialSH),
                            format = GraphicsFormat.R16G16B16A16_UNorm,
                        }
                    );

                    passData.SourceBlockingPotentialSH = builder.ReadBuffer(lpvData.PackedGridBuffers.BlockingPotentialSH);
                    passData.DestinationBlockingPotentialSH = builder.WriteTexture(lpvData.UnpackedGridTextures.BlockingPotentialSH);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _computeShader.GetKernelThreadGroupSizes(KernelIndex, out uint threadGroupSizeX, out uint _, out uint _);
            int totalCellCount = lpvData.GridSize * lpvData.GridSize * lpvData.GridSize;
            passData.ThreadGroupsX = AAAAMathUtils.AlignUp(totalCellCount, (int) threadGroupSizeX) / (int) threadGroupSizeX;
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            CoreUtils.SetKeyword(context.cmd, _computeShader, ShaderKeywords.BLOCKING_POTENTIAL, _type == PassType.BlockingPotential);

            switch (_type)
            {
                case PassType.Radiance:
                {
                    context.cmd.SetComputeBufferParam(_computeShader, KernelIndex, ShaderIDs._SourceRedSH, data.SourceRedSH);
                    context.cmd.SetComputeBufferParam(_computeShader, KernelIndex, ShaderIDs._SourceGreenSH, data.SourceGreenSH);
                    context.cmd.SetComputeBufferParam(_computeShader, KernelIndex, ShaderIDs._SourceBlueSH, data.SourceBlueSH);
                    context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._DestinationRedSH, data.DestinationRedSH);
                    context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._DestinationGreenSH, data.DestinationGreenSH);
                    context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._DestinationBlueSH, data.DestinationBlueSH);
                    break;
                }
                case PassType.BlockingPotential:
                {
                    context.cmd.SetComputeBufferParam(_computeShader, KernelIndex, ShaderIDs._SourceBlockingPotentialSH, data.SourceBlockingPotentialSH);
                    context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._DestinationBlockingPotentialSH,
                        data.DestinationBlockingPotentialSH
                    );
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            context.cmd.DispatchCompute(_computeShader, KernelIndex, data.ThreadGroupsX, 1, 1);

            if (_type == PassType.Radiance)
            {
                context.cmd.SetGlobalTexture(GlobalShaderIDs._LPVGridRedSH, data.DestinationRedSH);
                context.cmd.SetGlobalTexture(GlobalShaderIDs._LPVGridGreenSH, data.DestinationGreenSH);
                context.cmd.SetGlobalTexture(GlobalShaderIDs._LPVGridBlueSH, data.DestinationBlueSH);
            }
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