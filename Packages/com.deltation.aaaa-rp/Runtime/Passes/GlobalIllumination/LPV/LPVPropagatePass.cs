using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using DELTation.AAAARP.Volumes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.GlobalIllumination.LPV
{
    public class LPVPropagatePass : AAAARenderPass<LPVPropagatePass.PassData>
    {
        private const int KernelIndex = 0;
        private readonly ComputeShader _computeShader;

        public LPVPropagatePass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders shaders) : base(renderPassEvent) =>
            _computeShader = shaders.LpvPropagateCS;

        public override string Name => "LPV.Propagate";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();

            passData.PassCount = cameraData.VolumeStack.GetComponent<AAAALpvVolumeComponent>().PropagationPasses.value;
            if (passData.PassCount == 0)
            {
                return;
            }

            AAAALightPropagationVolumesData lpvData = frameData.Get<AAAALightPropagationVolumesData>();

            passData.GridSize = lpvData.GridSize;

            _computeShader.GetKernelThreadGroupSizes(KernelIndex, out passData.ThreadGroupSize, out uint _, out uint _);

            passData.Grid = new GridTextureSet
            {
                RedSH = builder.ReadWriteTexture(lpvData.GridRedSH),
                GreenSH = builder.ReadWriteTexture(lpvData.GridGreenSH),
                BlueSH = builder.ReadWriteTexture(lpvData.GridBlueSH),
            };
            passData.TempGrid = new GridTextureSet
            {
                RedSH = CreateTempGridTexture(builder, lpvData, nameof(PassData.TempGrid) + "_" + nameof(GridTextureSet.RedSH)),
                GreenSH = CreateTempGridTexture(builder, lpvData, nameof(PassData.TempGrid) + "_" + nameof(GridTextureSet.GreenSH)),
                BlueSH = CreateTempGridTexture(builder, lpvData, nameof(PassData.TempGrid) + "_" + nameof(GridTextureSet.BlueSH)),
            };
            passData.BlockingPotentialSH = builder.ReadTexture(lpvData.GridBlockingPotentialSH);
            return;

            static TextureHandle CreateTempGridTexture(RenderGraphBuilder builder, AAAALightPropagationVolumesData lpvData, string name) =>
                builder.CreateTransientTexture(new TextureDesc(lpvData.GridSHDesc) { name = name });
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._BlockingPotentialSH, data.BlockingPotentialSH);

            for (int i = 0; i < data.PassCount; ++i)
            {
                RenderPass(data, context, data.Grid, data.TempGrid);
                RenderPass(data, context, data.TempGrid, data.Grid);
            }
        }

        private void RenderPass(PassData data, RenderGraphContext context, in GridTextureSet source, in GridTextureSet destination)
        {
            if (data.ThreadGroupSize <= 0)
            {
                return;
            }

            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._SourceRedSH, source.RedSH);
            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._SourceGreenSH, source.GreenSH);
            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._SourceBlueSH, source.BlueSH);
            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._DestinationRedSH, destination.RedSH);
            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._DestinationGreenSH, destination.GreenSH);
            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._DestinationBlueSH, destination.BlueSH);

            int totalCellCount = data.GridSize * data.GridSize * data.GridSize;
            int threadGroupSize = (int) data.ThreadGroupSize;
            context.cmd.DispatchCompute(_computeShader, KernelIndex,
                AAAAMathUtils.AlignUp(totalCellCount, threadGroupSize) / threadGroupSize, 1, 1
            );
        }

        public class PassData : PassDataBase
        {
            public TextureHandle BlockingPotentialSH;
            public GridTextureSet Grid;
            public int GridSize;
            public int PassCount;
            public GridTextureSet TempGrid;
            public uint ThreadGroupSize;
        }

        public struct GridTextureSet
        {
            public TextureHandle BlueSH;
            public TextureHandle GreenSH;
            public TextureHandle RedSH;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static readonly int _SourceRedSH = Shader.PropertyToID(nameof(_SourceRedSH));
            public static readonly int _SourceGreenSH = Shader.PropertyToID(nameof(_SourceGreenSH));
            public static readonly int _SourceBlueSH = Shader.PropertyToID(nameof(_SourceBlueSH));
            public static readonly int _DestinationRedSH = Shader.PropertyToID(nameof(_DestinationRedSH));
            public static readonly int _DestinationGreenSH = Shader.PropertyToID(nameof(_DestinationGreenSH));
            public static readonly int _DestinationBlueSH = Shader.PropertyToID(nameof(_DestinationBlueSH));
            public static readonly int _BlockingPotentialSH = Shader.PropertyToID(nameof(_BlockingPotentialSH));
        }
    }
}