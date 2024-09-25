using DELTation.AAAARP.Debugging;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.FrameData
{
    public class AAAADebugData : ContextItem
    {
        public BufferHandle GPUCullingDebugBuffer;

        public void Init(RenderGraph renderGraph)
        {
            const int bufferDimension = (int) AAAAGPUCullingDebugData.GPUCullingDebugBufferDimension;
            const int count = bufferDimension * bufferDimension;
            var bufferDesc = new BufferDesc(count, UnsafeUtility.SizeOf<AAAAGPUCullingDebugData>(), GraphicsBuffer.Target.Structured)
            {
                name = nameof(GPUCullingDebugBuffer),
            };
            GPUCullingDebugBuffer = renderGraph.CreateBuffer(bufferDesc);
        }

        public override void Reset()
        {
            GPUCullingDebugBuffer = BufferHandle.nullHandle;
        }
    }
}