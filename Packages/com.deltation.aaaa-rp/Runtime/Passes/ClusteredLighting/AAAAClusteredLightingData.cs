using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using static DELTation.AAAARP.Passes.ClusteredLighting.AAAAClusteredLightingConstantBuffer;

namespace DELTation.AAAARP.Passes.ClusteredLighting
{
    public class AAAAClusteredLightingData : ContextItem
    {
        public BufferHandle LightGridBuffer;
        public BufferHandle LightIndexListBuffer;

        public void Init(RenderGraph renderGraph)
        {
            {
                var bufferDesc = new BufferDesc(
                    TotalClusters, UnsafeUtility.SizeOf<AAAAClusteredLightingGridCell>(), GraphicsBuffer.Target.Raw
                )
                {
                    name = nameof(LightGridBuffer),
                };
                LightGridBuffer = renderGraph.CreateBuffer(bufferDesc);
            }

            {
                var bufferDesc = new BufferDesc(
                    TotalClusters * MaxLightsPerCluster, sizeof(uint), GraphicsBuffer.Target.Structured
                )
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