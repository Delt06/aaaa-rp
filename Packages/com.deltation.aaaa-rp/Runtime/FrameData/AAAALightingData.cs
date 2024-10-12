using DELTation.AAAARP.Data;
using DELTation.AAAARP.Lighting;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.FrameData
{
    public class AAAALightingData : ContextItem
    {
        public float AmbientIntensity;
        public TextureHandle GTAOTerm;
        public AAAALightingConstantBuffer LightingConstantBuffer;
        public BufferHandle PunctualLightsBuffer;
        public TextureHandle SSRTraceResult;

        public void Init(RenderGraph renderGraph, AAAALightingSettings lightingSettings)
        {
            {
                var bufferDesc = new BufferDesc(lightingSettings.MaxPunctualLights, UnsafeUtility.SizeOf<AAAAPunctualLightData>(),
                    GraphicsBuffer.Target.Structured
                )
                {
                    name = nameof(PunctualLightsBuffer),
                };
                PunctualLightsBuffer = renderGraph.CreateBuffer(bufferDesc);
            }

            GTAOTerm = TextureHandle.nullHandle;
            SSRTraceResult = TextureHandle.nullHandle;
        }

        public override void Reset()
        {
            AmbientIntensity = default;
            LightingConstantBuffer = default;
            PunctualLightsBuffer = BufferHandle.nullHandle;
            GTAOTerm = TextureHandle.nullHandle;
            SSRTraceResult = TextureHandle.nullHandle;
        }
    }
}