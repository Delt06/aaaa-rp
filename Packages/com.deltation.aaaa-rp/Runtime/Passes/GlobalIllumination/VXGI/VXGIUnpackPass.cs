using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using UnityEngine;
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
            passData.DestinationAlbedo = builder.WriteTexture(vxgiData.GridAlbedo);
            passData.DestinationEmission = builder.WriteTexture(vxgiData.GridEmission);
            passData.DestinationNormals = builder.WriteTexture(vxgiData.GridNormals);

            builder.AllowPassCulling(false);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            if (data.ThreadGroups == 0)
            {
                return;
            }

            context.cmd.SetComputeBufferParam(_computeShader, KernelSize, ShaderID._Source, data.Source);
            context.cmd.SetComputeTextureParam(_computeShader, KernelSize, ShaderID._DestinationAlbedo, data.DestinationAlbedo);
            context.cmd.SetComputeTextureParam(_computeShader, KernelSize, ShaderID._DestinationEmission, data.DestinationEmission);
            context.cmd.SetComputeTextureParam(_computeShader, KernelSize, ShaderID._DestinationNormals, data.DestinationNormals);
            context.cmd.DispatchCompute(_computeShader, KernelSize, data.ThreadGroups, 1, 1);
        }

        public class PassData : PassDataBase
        {
            public TextureHandle DestinationAlbedo;
            public TextureHandle DestinationEmission;
            public TextureHandle DestinationNormals;
            public BufferHandle Source;
            public int ThreadGroups;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderID
        {
            public static readonly int _Source = Shader.PropertyToID(nameof(_Source));
            public static readonly int _DestinationAlbedo = Shader.PropertyToID(nameof(_DestinationAlbedo));
            public static readonly int _DestinationEmission = Shader.PropertyToID(nameof(_DestinationEmission));
            public static readonly int _DestinationNormals = Shader.PropertyToID(nameof(_DestinationNormals));
        }
    }
}