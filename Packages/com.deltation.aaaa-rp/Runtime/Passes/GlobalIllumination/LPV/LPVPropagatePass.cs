using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using DELTation.AAAARP.Volumes;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using static DELTation.AAAARP.Lighting.AAAALightPropagationVolumes;

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

            AAAALpvVolumeComponent lpvVolumeComponent = cameraData.VolumeStack.GetComponent<AAAALpvVolumeComponent>();
            passData.PassCount = lpvVolumeComponent.PropagationPasses.value;
            if (passData.PassCount == 0)
            {
                return;
            }

            AAAALightPropagationVolumesData lpvData = frameData.Get<AAAALightPropagationVolumesData>();

            passData.BlockingPotential = lpvData.BlockingPotential;
            passData.GridSize = lpvData.GridSize;
            passData.OcclusionAmplification = lpvVolumeComponent.OcclusionAmplification.value;

            ref readonly AAAALightPropagationVolumesData.GridBufferSet gridBuffers = ref lpvData.PackedGridBuffers;
            passData.Grid = new GridBufferSet
            {
                RedSH = builder.WriteBuffer(gridBuffers.RedSH),
                GreenSH = builder.WriteBuffer(gridBuffers.GreenSH),
                BlueSH = builder.WriteBuffer(gridBuffers.BlueSH),
            };
            passData.TempGrid = new GridBufferSet
            {
                RedSH = CreateTempGridBuffer(builder, gridBuffers.SHDesc, nameof(PassData.TempGrid) + "_" + nameof(GridBufferSet.RedSH)),
                GreenSH = CreateTempGridBuffer(builder, gridBuffers.SHDesc, nameof(PassData.TempGrid) + "_" + nameof(GridBufferSet.GreenSH)),
                BlueSH = CreateTempGridBuffer(builder, gridBuffers.SHDesc, nameof(PassData.TempGrid) + "_" + nameof(GridBufferSet.BlueSH)),
            };
            passData.BlockingPotentialSH = passData.BlockingPotential ? builder.ReadBuffer(gridBuffers.BlockingPotentialSH) : default;

            _computeShader.GetKernelThreadGroupSizes(KernelIndex, out passData.ThreadGroupSize, out uint _, out uint _);
            return;

            static BufferHandle CreateTempGridBuffer(RenderGraphBuilder builder, BufferDesc desc, string name)
            {
                desc.name = name;
                return builder.CreateTransientBuffer(desc);
            }
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            CoreUtils.SetKeyword(context.cmd, _computeShader, ShaderKeywords.BLOCKING_POTENTIAL, data.BlockingPotential);
            if (data.BlockingPotential)
            {
                context.cmd.SetComputeBufferParam(_computeShader, KernelIndex, ShaderIDs._BlockingPotentialSH, data.BlockingPotentialSH);
                context.cmd.SetComputeFloatParam(_computeShader, ShaderIDs._OcclusionAmplification, math.pow(2, data.OcclusionAmplification));
            }

            for (int i = 0; i < data.PassCount; ++i)
            {
                RenderPass(data, context, data.Grid, data.TempGrid);
                RenderPass(data, context, data.TempGrid, data.Grid);
            }
        }

        private void RenderPass(PassData data, RenderGraphContext context, in GridBufferSet source, in GridBufferSet destination)
        {
            if (data.ThreadGroupSize <= 0)
            {
                return;
            }

            context.cmd.SetComputeBufferParam(_computeShader, KernelIndex, ShaderIDs._SourceRedSH, source.RedSH);
            context.cmd.SetComputeBufferParam(_computeShader, KernelIndex, ShaderIDs._SourceGreenSH, source.GreenSH);
            context.cmd.SetComputeBufferParam(_computeShader, KernelIndex, ShaderIDs._SourceBlueSH, source.BlueSH);
            context.cmd.SetComputeBufferParam(_computeShader, KernelIndex, ShaderIDs._DestinationRedSH, destination.RedSH);
            context.cmd.SetComputeBufferParam(_computeShader, KernelIndex, ShaderIDs._DestinationGreenSH, destination.GreenSH);
            context.cmd.SetComputeBufferParam(_computeShader, KernelIndex, ShaderIDs._DestinationBlueSH, destination.BlueSH);

            int totalCellCount = data.GridSize * data.GridSize * data.GridSize;
            int threadGroupSize = (int) data.ThreadGroupSize;
            context.cmd.DispatchCompute(_computeShader, KernelIndex,
                AAAAMathUtils.AlignUp(totalCellCount, threadGroupSize) / threadGroupSize, 1, 1
            );
        }

        public class PassData : PassDataBase
        {
            public bool BlockingPotential;
            public BufferHandle BlockingPotentialSH;
            public GridBufferSet Grid;
            public int GridSize;
            public float OcclusionAmplification;
            public int PassCount;
            public GridBufferSet TempGrid;
            public uint ThreadGroupSize;
        }

        public struct GridBufferSet
        {
            public BufferHandle BlueSH;
            public BufferHandle GreenSH;
            public BufferHandle RedSH;
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
            public static readonly int _OcclusionAmplification = Shader.PropertyToID(nameof(_OcclusionAmplification));
        }
    }
}