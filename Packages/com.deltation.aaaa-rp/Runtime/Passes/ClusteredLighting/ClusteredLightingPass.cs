using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.ClusteredLighting
{
    public sealed class ClusteredLightingPass : AAAARenderPass<ClusteredLightingPass.PassData>
    {
        private readonly ComputeShader _buildClusterGridCS;

        public ClusteredLightingPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders runtimeShaders) : base(renderPassEvent) =>
            _buildClusterGridCS = runtimeShaders.BuildClusterGridCS;

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAAClusteredLightingData clusteredLightingData = frameData.GetOrCreate<AAAAClusteredLightingData>();
            clusteredLightingData.Init(renderingData.RenderGraph);

            {
                const int totalClusters = AAAAClusteredLightingConstantBuffer.TotalClusters;
                var bufferDesc = new BufferDesc(totalClusters, UnsafeUtility.SizeOf<AAAAClusterBounds>(), GraphicsBuffer.Target.Structured)
                {
                    name = nameof(PassData.ClusterBoundsBuffer),
                };
                passData.ClusterBoundsBuffer = builder.CreateTransientBuffer(bufferDesc);
            }

            float2 scaledResolution = math.float2(cameraData.ScaledWidth, cameraData.ScaledHeight);
            float2 tileCount = math.float2(AAAAClusteredLightingConstantBuffer.ClustersX, AAAAClusteredLightingConstantBuffer.ClustersY);
            passData.TileSizeInPixels = math.float4(math.ceil(scaledResolution / tileCount), 0, 0);

            builder.AllowPassCulling(false);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            using (new ProfilingScope(context.cmd, Profiling.BuildClusterGrid))
            {
                const int kernelIndex = 0;
                const int threadGroupSize = AAAAClusteredLightingComputeShaders.BuildClusterGridThreadGroupSize;
                context.cmd.SetComputeBufferParam(_buildClusterGridCS, kernelIndex, ShaderIDs.BuildClusterGrid._ClusterBounds, data.ClusterBoundsBuffer);
                context.cmd.SetComputeVectorParam(_buildClusterGridCS, ShaderIDs.BuildClusterGrid._TileSizeInPixels, data.TileSizeInPixels);
                context.cmd.DispatchCompute(_buildClusterGridCS, kernelIndex,
                    AAAAMathUtils.AlignUp(AAAAClusteredLightingConstantBuffer.TotalClusters, threadGroupSize) / threadGroupSize,
                    1, 1
                );
            }
        }

        public class PassData : PassDataBase
        {
            public BufferHandle ClusterBoundsBuffer;
            public Vector4 TileSizeInPixels;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static class BuildClusterGrid
            {
                public static readonly int _ClusterBounds = Shader.PropertyToID(nameof(_ClusterBounds));
                public static readonly int _TileSizeInPixels = Shader.PropertyToID(nameof(_TileSizeInPixels));
            }
        }

        private static class Profiling
        {
            public static readonly ProfilingSampler BuildClusterGrid = new(nameof(BuildClusterGrid));
        }
    }
}