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
        private static readonly ShaderTagId GBufferLightMode = new("GBuffer");

        private static readonly ShaderTagId[] ForwardLightModes = { new("SRPDefaultUnlit"), new("ForwardLit") };

        public AAAARendererList GBuffer;
        public AAAARendererList Transparent;
        public AAAARendererList VisibilityBufferFalseNegative;
        public AAAARendererList VisibilityBufferMain;

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

            {
                const PerObjectData perObjectData = PerObjectData.None;
                RenderQueueRange renderQueueRange = RenderQueueRange.opaque;
                const SortingCriteria sortingCriteria = SortingCriteria.CommonOpaque;
                RendererListDesc desc = AAAARenderingUtils.CreateRendererListDesc(
                    cullingResults, cameraData.Camera,
                    GBufferLightMode, perObjectData, renderQueueRange, sortingCriteria
                );
                GBuffer = Create(renderGraph, desc);
            }

            {
                const PerObjectData perObjectData = PerObjectData.None;
                RenderQueueRange renderQueueRange = RenderQueueRange.transparent;
                const SortingCriteria sortingCriteria = SortingCriteria.CommonTransparent;
                RendererListDesc desc = AAAARenderingUtils.CreateRendererListDesc(
                    cullingResults, cameraData.Camera,
                    ForwardLightModes, perObjectData, renderQueueRange, sortingCriteria
                );
                Transparent = Create(renderGraph, desc);
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