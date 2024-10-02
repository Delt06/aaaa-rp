using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Utils;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.ClusteredLighting
{
    public sealed class ClusteredLightingPass : AAAARenderPass<ClusteredLightingPass.PassData>
    {
        private readonly ComputeShader _buildClusterGridCS;
        private readonly ComputeShader _clusterCullingCS;
        private readonly ComputeShader _rawBufferClearCS;

        public ClusteredLightingPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders runtimeShaders) : base(renderPassEvent)
        {
            _buildClusterGridCS = runtimeShaders.BuildClusterGridCS;
            _clusterCullingCS = runtimeShaders.ClusterCullingCS;
            _rawBufferClearCS = runtimeShaders.RawBufferClearCS;
        }

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAAClusteredLightingData clusteredLightingData = frameData.GetOrCreate<AAAAClusteredLightingData>();
            clusteredLightingData.Init(renderingData.RenderGraph);

            {
                passData.LightGridBuffer = builder.WriteBuffer(clusteredLightingData.LightGridBuffer);
                passData.LightIndexListBuffer = builder.WriteBuffer(clusteredLightingData.LightIndexListBuffer);
            }

            {
                const int totalClusters = AAAAClusteredLightingConstantBuffer.TotalClusters;
                var bufferDesc = new BufferDesc(totalClusters, UnsafeUtility.SizeOf<AAAAClusterBounds>(), GraphicsBuffer.Target.Structured)
                {
                    name = nameof(PassData.ClusterBoundsBuffer),
                };
                passData.ClusterBoundsBuffer = builder.CreateTransientBuffer(bufferDesc);
            }

            {
                var bufferDesc = new BufferDesc(1, sizeof(uint), GraphicsBuffer.Target.Raw)
                {
                    name = nameof(PassData.LightIndexCounterBuffer),
                };
                passData.LightIndexCounterBuffer = builder.CreateTransientBuffer(bufferDesc);
            }

            float2 scaledResolution = math.float2(cameraData.ScaledWidth, cameraData.ScaledHeight);
            float2 tileCount = math.float2(AAAAClusteredLightingConstantBuffer.ClustersX, AAAAClusteredLightingConstantBuffer.ClustersY);
            passData.TileSizeInPixels = math.float4(math.ceil(scaledResolution / tileCount), 0, 0);

            builder.AllowPassCulling(false);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            {
                AAAARawBufferClear.DispatchClear(context.cmd, _rawBufferClearCS, data.LightIndexCounterBuffer, 1, 0, 0);
            }

            using (new ProfilingScope(context.cmd, Profiling.BuildClusterGrid))
            {
                const int kernelIndex = 0;
                const int threadGroupSize = AAAAClusteredLightingComputeShaders.BuildClusterGridThreadGroupSize;
                context.cmd.SetComputeBufferParam(_buildClusterGridCS, kernelIndex, ShaderIDs._ClusterBounds, data.ClusterBoundsBuffer);
                context.cmd.SetComputeVectorParam(_buildClusterGridCS, ShaderIDs.BuildClusterGrid._TileSizeInPixels, data.TileSizeInPixels);
                context.cmd.DispatchCompute(_buildClusterGridCS, kernelIndex,
                    AAAAMathUtils.AlignUp(AAAAClusteredLightingConstantBuffer.TotalClusters, threadGroupSize) / threadGroupSize, 1, 1
                );
            }

            using (new ProfilingScope(context.cmd, Profiling.ClusterCulling))
            {
                const int kernelIndex = 0;
                const int threadGroupSize = AAAAClusteredLightingComputeShaders.ClusterCullingThreadGroupSize;
                context.cmd.SetComputeBufferParam(_clusterCullingCS, kernelIndex, ShaderIDs._ClusterBounds, data.ClusterBoundsBuffer);
                context.cmd.SetComputeBufferParam(_clusterCullingCS, kernelIndex, ShaderIDs.ClusterCulling._LightIndexCounter, data.LightIndexCounterBuffer);
                context.cmd.SetComputeBufferParam(_clusterCullingCS, kernelIndex, ShaderIDs.ClusterCulling._LightGrid, data.LightGridBuffer);
                context.cmd.SetComputeBufferParam(_clusterCullingCS, kernelIndex, ShaderIDs.ClusterCulling._LightIndexList, data.LightIndexListBuffer);

                Assert.IsTrue(AAAAClusteredLightingConstantBuffer.TotalClusters % threadGroupSize == 0);
                context.cmd.DispatchCompute(_clusterCullingCS, kernelIndex,
                    AAAAClusteredLightingConstantBuffer.TotalClusters / threadGroupSize, 1, 1
                );
            }

            context.cmd.SetGlobalBuffer(ShaderIDs.Global._ClusteredLightGrid, data.LightGridBuffer);
            context.cmd.SetGlobalBuffer(ShaderIDs.Global._ClusteredLightIndexList, data.LightIndexListBuffer);
        }

        public class PassData : PassDataBase
        {
            public BufferHandle ClusterBoundsBuffer;
            public BufferHandle LightGridBuffer;
            public BufferHandle LightIndexCounterBuffer;
            public BufferHandle LightIndexListBuffer;
            public Vector4 TileSizeInPixels;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static readonly int _ClusterBounds = Shader.PropertyToID(nameof(_ClusterBounds));

            public static class BuildClusterGrid
            {
                public static readonly int _TileSizeInPixels = Shader.PropertyToID(nameof(_TileSizeInPixels));
            }

            public static class ClusterCulling
            {
                public static readonly int _LightIndexCounter = Shader.PropertyToID(nameof(_LightIndexCounter));
                public static readonly int _LightGrid = Shader.PropertyToID(nameof(_LightGrid));
                public static readonly int _LightIndexList = Shader.PropertyToID(nameof(_LightIndexList));
            }

            public static class Global
            {
                public static readonly int _ClusteredLightGrid = Shader.PropertyToID(nameof(_ClusteredLightGrid));
                public static readonly int _ClusteredLightIndexList = Shader.PropertyToID(nameof(_ClusteredLightIndexList));
            }
        }

        private static class Profiling
        {
            public static readonly ProfilingSampler BuildClusterGrid = new(nameof(BuildClusterGrid));
            public static readonly ProfilingSampler ClusterCulling = new(nameof(ClusterCulling));
        }
    }
}