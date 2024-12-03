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
    }
}