using DELTation.AAAARP.Debugging;
using DELTation.AAAARP.FrameData;
using Unity.Collections;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.Debugging
{
    public sealed class GPUCullingDebugReadbackPass : AAAARenderPass<GPUCullingDebugReadbackPass.PassData>
    {
        public GPUCullingDebugReadbackPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent) { }

        public override string Name => "GPUCullingDebug.Readback";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAADebugData debugData = frameData.Get<AAAADebugData>();
            passData.Buffer = builder.ReadBuffer(debugData.GPUCullingDebugBuffer);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            context.cmd.RequestAsyncReadback(data.Buffer, request =>
                {
                    NativeArray<AAAAGPUCullingDebugData> readbackDebugData = request.GetData<AAAAGPUCullingDebugData>();

                    // int totalOcclusionCulledInstances = readbackDebugData.Select(d => (int) d.OcclusionCulledInstances).Sum();
                    // int totalOcclusionCulledMeshlets = readbackDebugData.Select(d => (int) d.OcclusionCulledMeshlets).Sum();
                    // Debug.Log($"Occlusion Culling: {totalOcclusionCulledInstances} instances, {totalOcclusionCulledMeshlets} meshlets.");
                }
            );
        }

        public class PassData : PassDataBase
        {
            public BufferHandle Buffer;
        }
    }
}