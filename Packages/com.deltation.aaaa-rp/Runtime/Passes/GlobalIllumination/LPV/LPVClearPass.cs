using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.GlobalIllumination.LPV
{
    public class LPVClearPass : AAAARenderPass<LPVClearPass.PassData>
    {
        private const int ClearCSKernelIndex = 0;

        private readonly ComputeShader _clearCS;

        public LPVClearPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders shaders) : base(renderPassEvent) =>
            _clearCS = shaders.LpvClearCS;

        public override string Name => "LPV.Clear";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAALightPropagationVolumesData lpvData = frameData.Get<AAAALightPropagationVolumesData>();

            passData.GridSize = lpvData.GridSize;

            ref readonly AAAALightPropagationVolumesData.GridBufferSet gridBuffers = ref lpvData.PackedGridBuffers;
            builder.WriteBuffer(passData.GridRedSH = gridBuffers.RedSH);
            builder.WriteBuffer(passData.GridGreenSH = gridBuffers.GreenSH);
            builder.WriteBuffer(passData.GridBlueSH = gridBuffers.BlueSH);
            builder.WriteBuffer(passData.GridBlockingPotentialSH = gridBuffers.BlockingPotentialSH);

            _clearCS.GetKernelThreadGroupSizes(ClearCSKernelIndex, out passData.ClearThreadGroupSizeX, out uint _, out uint _);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            // Clear
            {
                int threadGroupSizeX = (int) data.ClearThreadGroupSizeX;
                if (threadGroupSizeX > 0)
                {
                    context.cmd.SetComputeBufferParam(_clearCS, ClearCSKernelIndex, ShaderIDs._GridRedUAV, data.GridRedSH);
                    context.cmd.SetComputeBufferParam(_clearCS, ClearCSKernelIndex, ShaderIDs._GridGreenUAV, data.GridGreenSH);
                    context.cmd.SetComputeBufferParam(_clearCS, ClearCSKernelIndex, ShaderIDs._GridBlueUAV, data.GridBlueSH);
                    context.cmd.SetComputeBufferParam(_clearCS, ClearCSKernelIndex, ShaderIDs._GridBlockingPotentialUAV, data.GridBlockingPotentialSH);

                    int threadGroupsX = AAAAMathUtils.AlignUp(data.GridSize * data.GridSize * data.GridSize, threadGroupSizeX) / threadGroupSizeX;
                    context.cmd.DispatchCompute(_clearCS, ClearCSKernelIndex, threadGroupsX, 1, 1);
                }
            }
        }

        public class PassData : PassDataBase
        {
            public uint ClearThreadGroupSizeX;
            public BufferHandle GridBlockingPotentialSH;
            public BufferHandle GridBlueSH;
            public BufferHandle GridGreenSH;
            public BufferHandle GridRedSH;
            public int GridSize;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static readonly int _GridRedUAV = Shader.PropertyToID(nameof(_GridRedUAV));
            public static readonly int _GridGreenUAV = Shader.PropertyToID(nameof(_GridGreenUAV));
            public static readonly int _GridBlueUAV = Shader.PropertyToID(nameof(_GridBlueUAV));
            public static readonly int _GridBlockingPotentialUAV = Shader.PropertyToID(nameof(_GridBlockingPotentialUAV));
        }
    }
}