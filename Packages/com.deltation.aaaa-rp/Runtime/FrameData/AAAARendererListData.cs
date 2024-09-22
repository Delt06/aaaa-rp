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

        public AAAARendererList VisibilityBufferMain;
        public AAAARendererList VisibilityBufferFalseNegative;

        public void Init(AAAARenderingData renderingData, AAAACameraData cameraData)
        {
            RenderGraph renderGraph = renderingData.RenderGraph;
            CullingResults cullingResults = renderingData.CullingResults;

            {
                const PerObjectData perObjectData = PerObjectData.None;
                RenderQueueRange renderQueueRange = RenderQueueRange.opaque;
                const SortingCriteria sortingCriteria = SortingCriteria.None;
                RendererListDesc desc = AAAARenderingUtils.CreateRendererListDesc(
                    cullingResults, cameraData.Camera,
                    VisibilityBufferLightMode, perObjectData, renderQueueRange, sortingCriteria
                );
                VisibilityBufferMain = Create(renderGraph, desc);
                VisibilityBufferFalseNegative = Create(renderGraph, desc);
            }

        }

        private static AAAARendererList Create(RenderGraph renderGraph, RendererListDesc desc) =>
            new()
            {
                Desc = desc,
                Handle = renderGraph.CreateRendererList(desc),
            };

        public override void Reset()
        {
            VisibilityBufferMain = default;
        }
    }
}