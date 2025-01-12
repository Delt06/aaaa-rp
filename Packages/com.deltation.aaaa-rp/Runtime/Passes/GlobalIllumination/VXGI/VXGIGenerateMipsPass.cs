using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.GlobalIllumination.VXGI
{
    public class VXGIGenerateMipsPass : AAAARenderPass<VXGIGenerateMipsPass.PassData>
    {
        private const int KernelIndex = 0;
        private readonly ComputeShader _computeShader;

        public VXGIGenerateMipsPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent)
        {
            AAAAVxgiRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAAVxgiRuntimeShaders>();
            _computeShader = shaders.GenerateMips3dCS;
        }

        public override string Name => "VXGI.GenerateMips";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            _computeShader.GetKernelThreadGroupSizes(KernelIndex,
                out passData.ThreadGroupSize.x, out passData.ThreadGroupSize.y, out passData.ThreadGroupSize.z
            );

            AAAAVoxelGlobalIlluminationData vxgiData = frameData.Get<AAAAVoxelGlobalIlluminationData>();
            passData.Radiance = builder.ReadWriteTexture(vxgiData.GridRadiance);
            passData.MipCount = vxgiData.GridMipCount;
            passData.GridSize = vxgiData.GridSize;
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            for (int srcMip = 0; srcMip < data.MipCount - 1; srcMip++)
            {
                int dstMip = srcMip + 1;
                context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderID._SrcRadiance, data.Radiance, srcMip);
                context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderID._DstRadiance, data.Radiance, dstMip);

                int dstSize = data.GridSize >> dstMip;
                context.cmd.SetComputeIntParam(_computeShader, ShaderID._DstSize, dstSize);
                context.cmd.SetComputeIntParam(_computeShader, ShaderID._DstMip, dstMip);

                int3 threadGroups = AAAAMathUtils.AlignUp(dstSize, (int3) data.ThreadGroupSize) / (int3) data.ThreadGroupSize;
                context.cmd.DispatchCompute(_computeShader, KernelIndex, threadGroups.x, threadGroups.y, threadGroups.z);
            }
        }

        public class PassData : PassDataBase
        {
            public TextureHandle Radiance;
            public int GridSize;
            public int MipCount;
            public uint3 ThreadGroupSize;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderID
        {
            public static readonly int _DstSize = Shader.PropertyToID(nameof(_DstSize));
            public static readonly int _DstMip = Shader.PropertyToID(nameof(_DstMip));
            public static readonly int _SrcRadiance = Shader.PropertyToID(nameof(_SrcRadiance));
            public static readonly int _DstRadiance = Shader.PropertyToID(nameof(_DstRadiance));
        }
    }
}