using DELTation.AAAARP.Debugging;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Utils;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.Debugging
{
    public sealed class GPUCullingDebugSetupPass : AAAARenderPass<GPUCullingDebugSetupPass.PassData>
    {
        private readonly AAAARawBufferClear _rawBufferClear;

        public GPUCullingDebugSetupPass(AAAARenderPassEvent renderPassEvent, AAAARawBufferClear rawBufferClear)
            : base(renderPassEvent) => _rawBufferClear = rawBufferClear;

        public override string Name => "GPUCulling.Debug.Setup";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAADebugData debugData = frameData.Get<AAAADebugData>();
            passData.DebugBuffer = builder.WriteBuffer(debugData.GPUCullingDebugBuffer);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            int bufferStride = UnsafeUtility.SizeOf<AAAAGPUCullingDebugData>() / sizeof(uint);
            const int bufferCount = (int) (AAAAGPUCullingDebugData.GPUCullingDebugBufferDimension * AAAAGPUCullingDebugData.GPUCullingDebugBufferDimension);
            int uintCount = bufferCount * bufferStride;
            const int writeOffset = 0;
            const int clearValue = 0;
            _rawBufferClear.DispatchClear(context.cmd,
                data.DebugBuffer, uintCount, writeOffset, clearValue
            );
        }

        public class PassData : PassDataBase
        {
            public BufferHandle DebugBuffer;
        }
    }
}