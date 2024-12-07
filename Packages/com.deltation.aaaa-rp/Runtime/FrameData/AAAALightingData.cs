using DELTation.AAAARP.Data;
using DELTation.AAAARP.Lighting;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.FrameData
{
    public class AAAALightingData : ContextItem
    {
        public float AmbientIntensity;
        public TextureHandle DeferredReflections;
        public TextureHandle GTAOTerm;
        public AAAALightingConstantBuffer LightingConstantBuffer;
        public TextureHandle LPVGridRedSH;
        public TextureHandle LPVGridGreenSH;
        public TextureHandle LPVGridBlueSH;
        public float3 LPVGridBoundsMax;
        public float3 LPVGridBoundsMin;
        public int LPVGridSize;
        public BufferHandle PunctualLightsBuffer;
        public TextureHandle SSRResolveResult;
        public TextureHandle SSRTraceResult;
        public int2 SSRTraceResultSize;
        public TextureDesc LPVGridSHDesc;

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
            SSRResolveResult = TextureHandle.nullHandle;
            LPVGridSHDesc = default;
            LPVGridRedSH = LPVGridGreenSH = LPVGridBlueSH = TextureHandle.nullHandle;
            LPVGridBoundsMin = default;
            LPVGridBoundsMax = default;
            LPVGridSize = default;
        }

        public override void Reset()
        {
            AmbientIntensity = default;
            LightingConstantBuffer = default;
            PunctualLightsBuffer = BufferHandle.nullHandle;
            GTAOTerm = TextureHandle.nullHandle;
            SSRTraceResult = TextureHandle.nullHandle;
            SSRResolveResult = TextureHandle.nullHandle;
            SSRTraceResultSize = default;
            DeferredReflections = default;
            LPVGridSHDesc = default;
            LPVGridRedSH = LPVGridGreenSH = LPVGridBlueSH = TextureHandle.nullHandle;
            LPVGridBoundsMin = default;
            LPVGridBoundsMax = default;
            LPVGridSize = default;
        }
    }
}