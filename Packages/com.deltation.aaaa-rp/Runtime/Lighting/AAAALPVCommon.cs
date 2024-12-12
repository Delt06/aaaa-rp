using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Volumes;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Lighting
{
    public static class AAAALPVCommon
    {
        public const int RsmAttachmentsCount = 3;

        public const GraphicsFormat GridFormat = GraphicsFormat.R32G32B32A32_SFloat;
        public const GraphicsFormat GridBlockingPotentialFormat = GraphicsFormat.R16G16B16A16_SNorm;

        public static void CreateBounds(AAAACameraData cameraData, AAAALPVVolumeComponent lpvVolumeComponent, out float3 min, out float3 max)
        {
            float boundsSize = lpvVolumeComponent.BoundsSize.value;
            int gridSize = (int) lpvVolumeComponent.GridSize.value;
            float forwardBias = lpvVolumeComponent.BoundsForwardBias.value;

            float cellSize = boundsSize / gridSize;
            Transform cameraTransform = cameraData.Camera.transform;
            float3 center = (float3) cameraTransform.position + (float3) cameraTransform.forward * (boundsSize * forwardBias);
            min = center - boundsSize * 0.5f;
            min = math.floor(min / cellSize) * cellSize;
            max = min + math.ceil(boundsSize / cellSize) * cellSize;
        }

        public static RsmAttachmentAllocation AllocateRsmMaps(this in AAAARenderTexturePoolSet rtPoolSet, int resolution) =>
            new(rtPoolSet.RsmPositionMap.Allocate(resolution),
                rtPoolSet.RsmNormalMap.Allocate(resolution),
                rtPoolSet.RsmFluxMap.Allocate(resolution)
            );

        public static void LookupRsmAttachments(this in AAAARenderTexturePoolSet rtPoolSet, in RsmAttachmentAllocation rsmAttachmentAllocation,
            RenderTargetIdentifier[] renderTargetIdentifiers)
        {
            Assert.IsTrue(rsmAttachmentAllocation.IsValid);
            Assert.IsTrue(renderTargetIdentifiers.Length == RsmAttachmentsCount);

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
        }
    }
}