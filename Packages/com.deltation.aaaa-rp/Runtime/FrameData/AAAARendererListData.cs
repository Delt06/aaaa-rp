using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.FrameData
{
    public struct AAAARendererList
    {
        public RendererListHandle Handle;
        public RendererListDesc Desc;
    }

    public class AAAARendererListData : AAAAResourceDataBase
    {
        private static readonly ShaderTagId VisibilityBufferLightMode = new("Visibility");

        public AAAARendererList VisibilityBuffer;

        public void Init(AAAARenderingData renderingData, AAAACameraData cameraData)
        {
            RenderGraph renderGraph = renderingData.RenderGraph;
            CullingResults cullingResults = renderingData.CullingResults;

            VisibilityBuffer = Create(renderGraph,
                AAAARenderingUtils.CreateRendererListDesc(cullingResults, cameraData.Camera, VisibilityBufferLightMode, PerObjectData.None,
                    RenderQueueRange.opaque, SortingCriteria.None
                )
            );
        }

        private static AAAARendererList Create(RenderGraph renderGraph, RendererListDesc desc) =>
            new()
            {
                Desc = desc,
                Handle = renderGraph.CreateRendererList(desc),
            };

        public override void Reset()
        {
            VisibilityBuffer = default;
        }
    }
}