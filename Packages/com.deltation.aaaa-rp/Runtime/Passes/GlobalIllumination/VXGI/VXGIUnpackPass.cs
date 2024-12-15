using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.GlobalIllumination.VXGI
{
    public class VXGIUnpackPass : AAAARenderPass<VXGIUnpackPass.PassData>
    {
        private const int KernelSize = 0;
        private readonly ComputeShader _computeShader;

        public VXGIUnpackPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent)
        {
            AAAAVxgiRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAAVxgiRuntimeShaders>();
            _computeShader = shaders.UnpackCS;
        }

        public override string Name => "VXGI.Unpack";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAAVoxelGlobalIlluminationData vxgiData = frameData.Get<AAAAVoxelGlobalIlluminationData>();

            _computeShader.GetKernelThreadGroupSizes(KernelSize, out uint threadGroupSize, out uint _, out uint _);
            passData.ThreadGroups = threadGroupSize == 0
                ? 0
                : AAAAMathUtils.AlignUp(vxgiData.GridSize * vxgiData.GridSize * vxgiData.GridSize, (int) threadGroupSize) / (int) threadGroupSize;

            passData.Source = builder.ReadBuffer(vxgiData.PackedGridBuffer);
            passData.Destination = builder.WriteTexture(vxgiData.GridAlbedo);

            builder.AllowPassCulling(false);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            if (data.ThreadGroups == 0)
            {
                return;
            }

            context.cmd.SetComputeBufferParam(_computeShader, KernelSize, ShaderID._Source, data.Source);
            context.cmd.SetComputeTextureParam(_computeShader, KernelSize, ShaderID._Destination, data.Destination);
            context.cmd.DispatchCompute(_computeShader, KernelSize, data.ThreadGroups, 1, 1);
        }

        public class PassData : PassDataBase
        {
            public TextureHandle Destination;
            public BufferHandle Source;
            public int ThreadGroups;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderID
        {
            public static readonly int _Source = Shader.PropertyToID(nameof(_Source));
            public static readonly int _Destination = Shader.PropertyToID(nameof(_Destination));
        }
    }
}