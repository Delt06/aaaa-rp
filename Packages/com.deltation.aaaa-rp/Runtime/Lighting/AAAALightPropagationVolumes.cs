using System.Diagnostics.CodeAnalysis;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Lighting
{
    public static class AAAALightPropagationVolumes
    {
        public const int AttachmentsCount = 3;

        public static RsmAttachmentAllocation AllocateRsmMaps(this in AAAARenderTexturePoolSet rtPoolSet, int resolution) =>
            new(rtPoolSet.RsmPositionMap.Allocate(resolution),
                rtPoolSet.RsmNormalMap.Allocate(resolution),
                rtPoolSet.RsmFluxMap.Allocate(resolution)
            );

        public static void LookupRsmAttachments(this in AAAARenderTexturePoolSet rtPoolSet, in RsmAttachmentAllocation rsmAttachmentAllocation,
            RenderTargetIdentifier[] renderTargetIdentifiers)
        {
            Assert.IsTrue(rsmAttachmentAllocation.IsValid);
            Assert.IsTrue(renderTargetIdentifiers.Length == AttachmentsCount);

            renderTargetIdentifiers[0] = rtPoolSet.RsmPositionMap.LookupRenderTexture(rsmAttachmentAllocation.PositionsMap);
            renderTargetIdentifiers[1] = rtPoolSet.RsmNormalMap.LookupRenderTexture(rsmAttachmentAllocation.NormalMap);
            renderTargetIdentifiers[2] = rtPoolSet.RsmFluxMap.LookupRenderTexture(rsmAttachmentAllocation.FluxMap);
        }

        public struct RsmLight
        {
            public int VisibleLightIndex;
            public int ShadowLightIndex;
            public RsmAttachmentAllocation RenderedAllocation;
            public RsmAttachmentAllocation InjectedAllocation;
            public float4 DirectionWS;
            public float4 Color;
        }

        public struct RsmAttachmentAllocation
        {
            public readonly AAAARenderTexturePool.Allocation PositionsMap;
            public readonly AAAARenderTexturePool.Allocation NormalMap;
            public readonly AAAARenderTexturePool.Allocation FluxMap;
            public readonly bool IsValid;

            public RsmAttachmentAllocation(AAAARenderTexturePool.Allocation positionsMap, AAAARenderTexturePool.Allocation normalMap,
                AAAARenderTexturePool.Allocation fluxMap)
            {
                PositionsMap = positionsMap;
                NormalMap = normalMap;
                FluxMap = fluxMap;
                IsValid = true;
            }
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public static class GlobalShaderIDs
        {
            public static readonly int _LPVGridSize = Shader.PropertyToID(nameof(_LPVGridSize));
            public static readonly int _LPVGridBoundsMin = Shader.PropertyToID(nameof(_LPVGridBoundsMin));
            public static readonly int _LPVGridBoundsMax = Shader.PropertyToID(nameof(_LPVGridBoundsMax));
            public static readonly int _LPVGridRedSH = Shader.PropertyToID(nameof(_LPVGridRedSH));
            public static readonly int _LPVGridGreenSH = Shader.PropertyToID(nameof(_LPVGridGreenSH));
            public static readonly int _LPVGridBlueSH = Shader.PropertyToID(nameof(_LPVGridBlueSH));
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public static class ShaderKeywords
        {
            public static string BLOCKING_POTENTIAL = nameof(BLOCKING_POTENTIAL);
            public static string TRILINEAR_INTERPOLATION = nameof(TRILINEAR_INTERPOLATION);
        }
    }
}