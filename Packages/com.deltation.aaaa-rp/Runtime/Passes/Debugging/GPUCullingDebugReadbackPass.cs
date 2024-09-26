using System;
using DELTation.AAAARP.Debugging;
using DELTation.AAAARP.FrameData;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.Debugging
{
    public sealed class GPUCullingDebugReadbackPass : AAAARenderPass<GPUCullingDebugReadbackPass.PassData>
    {
        private readonly AAAARenderPipelineDebugDisplaySettings _displaySettings;

        public GPUCullingDebugReadbackPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineDebugDisplaySettings displaySettings) :
            base(renderPassEvent) => _displaySettings = displaySettings;

        public override string Name => "GPUCulling.Debug.Readback";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAADebugData debugData = frameData.Get<AAAADebugData>();
            passData.Buffer = builder.ReadBuffer(debugData.GPUCullingDebugBuffer);

            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            passData.Camera = cameraData.Camera;
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            Camera camera = data.Camera;

            context.cmd.RequestAsyncReadback(data.Buffer, request =>
                {
                    NativeArray<AAAAGPUCullingDebugData> readbackDebugData = request.GetData<AAAAGPUCullingDebugData>();
                    _displaySettings.DebugStats.GPUCulling[camera] = new AAAADebugStats.GPUCullingStats
                    {
                        Data = AggregateCullingDebugData(readbackDebugData),
                        LastUpdateTime = AAAADebugStats.TimeNow,
                    };
                }
            );
        }

        private static AAAAGPUCullingDebugData AggregateCullingDebugData(Span<AAAAGPUCullingDebugData> data)
        {
            var aggregateData = new AAAAGPUCullingDebugData();

            foreach (AAAAGPUCullingDebugData item in data)
            {
                aggregateData.OcclusionCulledInstances += item.OcclusionCulledInstances;
                aggregateData.OcclusionCulledMeshlets += item.OcclusionCulledMeshlets;
            }

            return aggregateData;
        }

        public class PassData : PassDataBase
        {
            public BufferHandle Buffer;
            public Camera Camera;
        }
    }
}