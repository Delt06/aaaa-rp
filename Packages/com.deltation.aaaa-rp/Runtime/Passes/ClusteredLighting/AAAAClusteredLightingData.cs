using DELTation.AAAARP.Data;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.ClusteredLighting
{
    public class AAAAClusteredLightingData : ContextItem
    {
        public BufferHandle LightGridBuffer;
        public BufferHandle LightIndexListBuffer;

        public void Init(RenderGraph renderGraph, AAAALightingSettings lightingSettings)
        {
            const int totalClusters = AAAAClusteredLightingConstantBuffer.TotalClusters;

            {
                var bufferDesc = new BufferDesc(totalClusters, sizeof(uint), GraphicsBuffer.Target.Raw)
                {
                    name = nameof(LightGridBuffer),
                };
                LightGridBuffer = renderGraph.CreateBuffer(bufferDesc);
            }

            {
                var bufferDesc = new BufferDesc(totalClusters * lightingSettings.MaxPunctualLightsPerCluster, sizeof(uint), GraphicsBuffer.Target.Raw)
                {
                    name = nameof(LightIndexListBuffer),
                };
                LightIndexListBuffer = renderGraph.CreateBuffer(bufferDesc);
            }
        }

        public override void Reset()
        {
            LightGridBuffer = BufferHandle.nullHandle;
            LightIndexListBuffer = BufferHandle.nullHandle;
        }
    }
}