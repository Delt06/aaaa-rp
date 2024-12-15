using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Lighting;
using DELTation.AAAARP.Utils;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.GlobalIllumination.VXGI
{
    public class VXGISetupPass : AAAARenderPass<VXGISetupPass.PassData>
    {
        private readonly AAAARawBufferClear _rawBufferClear;

        public VXGISetupPass(AAAARenderPassEvent renderPassEvent, AAAARawBufferClear rawBufferClear) : base(renderPassEvent) =>
            _rawBufferClear = rawBufferClear;

        public override string Name => "VXGI.Setup";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.GetOrCreate<AAAARenderingData>();
            AAAAVoxelGlobalIlluminationData vxgiData = frameData.GetOrCreate<AAAAVoxelGlobalIlluminationData>();

            const float boundsSize = 40.0f;
            vxgiData.GridSize = 64;
            vxgiData.BoundsMin = -boundsSize * 0.5f;
            vxgiData.BoundsMax = boundsSize * 0.5f;

            int packedBufferCount = vxgiData.GridSize * vxgiData.GridSize * vxgiData.GridSize * (int) AAAAVxgiPackedGridChannels.TotalCount;
            vxgiData.PackedGridBufferDesc = new BufferDesc(AAAAMathUtils.AlignUp(packedBufferCount, 4), sizeof(uint), GraphicsBuffer.Target.Raw)
            {
                name = AAAAVxgiCommon.ResourceNamePrefix + nameof(AAAAVoxelGlobalIlluminationData.PackedGridBuffer),
            };
            vxgiData.PackedGridBuffer = renderingData.RenderGraph.CreateBuffer(vxgiData.PackedGridBufferDesc);
            vxgiData.GridAlbedo = renderingData.RenderGraph.CreateTexture(new TextureDesc
                {
                    name = AAAAVxgiCommon.ResourceNamePrefix + nameof(AAAAVoxelGlobalIlluminationData.GridAlbedo),
                    width = vxgiData.GridSize,
                    height = vxgiData.GridSize,
                    slices = vxgiData.GridSize,
                    format = GraphicsFormat.R8G8B8A8_UNorm,
                    dimension = TextureDimension.Tex3D,
                    filterMode = FilterMode.Trilinear,
                    msaaSamples = MSAASamples.None,
                    enableRandomWrite = true,
                }
            );

            passData.PackedGridBufferCount = packedBufferCount;
            passData.PackedGridBuffer = builder.WriteBuffer(vxgiData.PackedGridBuffer);
            passData.ConstantBuffer = new AAAAVxgiConstantBuffer
            {
                _VxgiGridBoundsMin = math.float4(vxgiData.BoundsMin, 0),
                _VxgiGridBoundsMax = math.float4(vxgiData.BoundsMax, 0),
                _VxgiGridResolution = math.float4(vxgiData.GridSize, 1.0f / vxgiData.GridSize, boundsSize / vxgiData.GridSize, vxgiData.GridSize / boundsSize),
            };
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            ConstantBuffer.PushGlobal(context.cmd, data.ConstantBuffer, ShaderIDs.ConstantBufferID);

            const int writeOffset = 0;
            const int clearValue = 0;
            _rawBufferClear.DispatchClear(context.cmd, data.PackedGridBuffer, data.PackedGridBufferCount, writeOffset, clearValue);
        }

        public class PassData : PassDataBase
        {
            public AAAAVxgiConstantBuffer ConstantBuffer;
            public BufferHandle PackedGridBuffer;
            public int PackedGridBufferCount;
        }

        private static class ShaderIDs
        {
            public static readonly int ConstantBufferID = Shader.PropertyToID(nameof(AAAAVxgiConstantBuffer));
        }
    }

}