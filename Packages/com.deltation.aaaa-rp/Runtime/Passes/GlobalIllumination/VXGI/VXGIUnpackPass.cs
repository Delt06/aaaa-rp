using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using DELTation.AAAARP.Utils;
using DELTation.AAAARP.Volumes;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using static DELTation.AAAARP.Lighting.AAAAVxgiCommon;

namespace DELTation.AAAARP.Passes.GlobalIllumination.VXGI
{
    public class VXGIUnpackPass : AAAARenderPass<VXGIUnpackPass.PassData>
    {
        private const int KernelIndex = 0;
        private readonly ComputeShader _computeShader;

        public VXGIUnpackPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent)
        {
            AAAAVxgiRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAAVxgiRuntimeShaders>();
            _computeShader = shaders.UnpackCS;
        }

        public override string Name => "VXGI.Unpack";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAAVoxelGlobalIlluminationData vxgiData = frameData.Get<AAAAVoxelGlobalIlluminationData>();

            _computeShader.GetKernelThreadGroupSizes(KernelIndex, out passData.ThreadGroupSize, out uint _, out uint _);
            passData.ItemCount = vxgiData.GridSize * vxgiData.GridSize * vxgiData.GridSize;

            passData.Source = builder.ReadBuffer(vxgiData.PackedGridBuffer);
            passData.DestinationRadiance = builder.WriteTexture(vxgiData.GridRadiance);
            passData.DestinationNormals = builder.WriteTexture(vxgiData.GridNormals);
            passData.GridMipCount = vxgiData.GridMipCount;
            passData.OpacityFactor = cameraData.VolumeStack.GetComponent<AAAAVXGIVolumeComponent>().OpacityFactor.value;
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            if (data.ThreadGroupSize == 0)
            {
                return;
            }

            DispatchUnpack(data, context);

            context.cmd.SetGlobalTexture(GlobalShaderIDs._VXGIRadiance, data.DestinationRadiance);
            context.cmd.SetGlobalInt(GlobalShaderIDs._VXGILevelCount, data.GridMipCount);
            context.cmd.SetGlobalFloat(GlobalShaderIDs._VXGIOpacityFactor, data.OpacityFactor);
        }

        private void DispatchUnpack(PassData data, RenderGraphContext context)
        {
            context.cmd.SetComputeBufferParam(_computeShader, KernelIndex, ShaderID._Source, data.Source);
            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderID._DestinationRadiance, data.DestinationRadiance);
            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderID._DestinationNormals, data.DestinationNormals);

            int threadGroupSize = (int) data.ThreadGroupSize;
            int maxItemsPerDispatch = ComputeUtils.MaxThreadGroups * threadGroupSize;
            int itemOffset = 0;
            int remainingItemCount = data.ItemCount;

            while (remainingItemCount > 0)
            {
                int dispatchItemCount = math.min(maxItemsPerDispatch, remainingItemCount);
                int threadGroups = AAAAMathUtils.AlignUp(dispatchItemCount, threadGroupSize) / threadGroupSize;
                context.cmd.SetComputeIntParam(_computeShader, ShaderID._FlatIDOffset, itemOffset);
                Assert.IsTrue(threadGroups <= ComputeUtils.MaxThreadGroups, "VXGIUnpackPass: Thread group count exceeded.");
                context.cmd.DispatchCompute(_computeShader, KernelIndex, threadGroups, 1, 1);

                remainingItemCount -= dispatchItemCount;
                itemOffset += dispatchItemCount;
            }
        }

        public class PassData : PassDataBase
        {
            public TextureHandle DestinationNormals;
            public TextureHandle DestinationRadiance;
            public int GridMipCount;
            public int ItemCount;
            public float OpacityFactor;
            public BufferHandle Source;
            public uint ThreadGroupSize;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderID
        {
            public static readonly int _Source = Shader.PropertyToID(nameof(_Source));
            public static readonly int _DestinationRadiance = Shader.PropertyToID(nameof(_DestinationRadiance));
            public static readonly int _DestinationNormals = Shader.PropertyToID(nameof(_DestinationNormals));
            public static readonly int _FlatIDOffset = Shader.PropertyToID(nameof(_FlatIDOffset));
        }
    }
}