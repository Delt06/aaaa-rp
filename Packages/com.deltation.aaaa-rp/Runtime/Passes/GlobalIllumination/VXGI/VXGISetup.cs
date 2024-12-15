using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Lighting;
using DELTation.AAAARP.Utils;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.GlobalIllumination.VXGI
{
    public class VXGISetup : AAAARenderPass<VXGISetup.PassData>
    {
        private readonly AAAARawBufferClear _rawBufferClear;

        public VXGISetup(AAAARenderPassEvent renderPassEvent, AAAARawBufferClear rawBufferClear) : base(renderPassEvent) => _rawBufferClear = rawBufferClear;

        public override string Name => "VXGI.Setup";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.GetOrCreate<AAAARenderingData>();
            AAAAVoxelGlobalIlluminationData vxgiData = frameData.GetOrCreate<AAAAVoxelGlobalIlluminationData>();

            vxgiData.GridSize = 64;
            vxgiData.BoundsMin = math.float3(-20, -20, -20);
            vxgiData.BoundsMax = math.float3(20, 20, 20);

            int packedBufferCount = vxgiData.GridSize * vxgiData.GridSize * vxgiData.GridSize * (int) AAAAVxgiCommon.Channels.TotalCount;
            vxgiData.PackedGridBufferDesc = new BufferDesc(packedBufferCount, sizeof(uint), GraphicsBuffer.Target.Raw)
            {
                name = AAAAVxgiCommon.ResourceNamePrefix + nameof(AAAAVoxelGlobalIlluminationData.PackedGridBuffer),
            };
            vxgiData.PackedGridBuffer = renderingData.RenderGraph.CreateBuffer(vxgiData.PackedGridBufferDesc);

            passData.PackedGridBufferCount = packedBufferCount;
            passData.PackedGridBuffer = builder.WriteBuffer(vxgiData.PackedGridBuffer);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            const int writeOffset = 0;
            const int clearValue = 0;
            _rawBufferClear.DispatchClear(context.cmd, data.PackedGridBuffer, data.PackedGridBufferCount, writeOffset, clearValue);
        }

        public class PassData : PassDataBase
        {
            public BufferHandle PackedGridBuffer;
            public int PackedGridBufferCount;
        }
    }
}