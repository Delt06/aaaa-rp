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
using static DELTation.AAAARP.Passes.ClusteredLighting.AAAAClusteredLightingConstantBuffer;

namespace DELTation.AAAARP.Passes.ClusteredLighting
{
    public sealed class ClusteredLightingPass : AAAARenderPass<ClusteredLightingPass.PassData>
    {
        private readonly ComputeShader _buildClusterGridCS;
        private readonly ComputeShader _clusterCullingCS;
        private readonly ComputeShader _compactActiveClusterListCS;
        private readonly ComputeShader _findActiveClustersCS;
        private readonly ComputeShader _fixupClusterCullingIndirectDispatchArgsCS;
        private readonly ComputeShader _rawBufferClearCS;

        public ClusteredLightingPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders runtimeShaders) : base(renderPassEvent)
        {
            _buildClusterGridCS = runtimeShaders.BuildClusterGridCS;
            _findActiveClustersCS = runtimeShaders.FindActiveClustersCS;
            _compactActiveClusterListCS = runtimeShaders.CompactActiveClusterListCS;
            _fixupClusterCullingIndirectDispatchArgsCS = runtimeShaders.FixupClusterCullingIndirectDispatchArgsCS;
            _clusterCullingCS = runtimeShaders.ClusterCullingCS;
            _rawBufferClearCS = runtimeShaders.RawBufferClearCS;
        }

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAAClusteredLightingData clusteredLightingData = frameData.GetOrCreate<AAAAClusteredLightingData>();
            clusteredLightingData.Init(renderingData.RenderGraph);

            {
                passData.LightGridBuffer = builder.WriteBuffer(clusteredLightingData.LightGridBuffer);
                passData.LightIndexListBuffer = builder.WriteBuffer(clusteredLightingData.LightIndexListBuffer);
            }

            {
                const int bitsInMask = 32;
                int count = AAAAMathUtils.AlignUp(TotalClusters, bitsInMask) / bitsInMask;
                var bufferDesc = new BufferDesc(count, sizeof(uint), GraphicsBuffer.Target.Raw)
                {
                    name = nameof(PassData.ActiveClustersMaskBuffer),
                };
                passData.ActiveClustersMaskBuffer = builder.CreateTransientBuffer(bufferDesc);
                passData.ActiveClustersMaskBufferCount = count;
            }

            {
                var bufferDesc = new BufferDesc(1, sizeof(uint), GraphicsBuffer.Target.Raw)
                {
                    name = nameof(PassData.ActiveClustersCountBuffer),
                };
                passData.ActiveClustersCountBuffer = builder.CreateTransientBuffer(bufferDesc);
            }

            {
                var bufferDesc = new BufferDesc(TotalClusters, sizeof(uint), GraphicsBuffer.Target.Raw)
                {
                    name = nameof(PassData.ActiveClustersListBuffer),
                };
                passData.ActiveClustersListBuffer = builder.CreateTransientBuffer(bufferDesc);
            }

            {
                var bufferDesc = new BufferDesc(UnsafeUtility.SizeOf<IndirectDispatchArgs>() / sizeof(uint), sizeof(uint),
                    GraphicsBuffer.Target.IndirectArguments
                )
                {
                    name = nameof(PassData.ClusterCullingIndirectArgsBuffer),
                };
                passData.ClusterCullingIndirectArgsBuffer = builder.CreateTransientBuffer(bufferDesc);
            }

            {
                var bufferDesc = new BufferDesc(TotalClusters, UnsafeUtility.SizeOf<AAAAClusterBounds>(), GraphicsBuffer.Target.Structured)
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

            int2 scaledResolution = math.int2(cameraData.ScaledWidth, cameraData.ScaledHeight);
            float2 tileCount = math.float2(ClustersX, ClustersY);
            passData.TileSizeInPixels = math.float4(math.ceil(scaledResolution / tileCount), 0, 0);
            passData.ScaledResolution = scaledResolution;

            builder.ReadTexture(resourceData.CameraScaledDepthBuffer);
            builder.AllowPassCulling(false);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            using (new ProfilingScope(context.cmd, Profiling.Prepare))
            {
                AAAARawBufferClear.DispatchClear(context.cmd, _rawBufferClearCS, data.LightIndexCounterBuffer, 1, 0, 0);
                AAAARawBufferClear.DispatchClear(context.cmd, _rawBufferClearCS, data.ActiveClustersCountBuffer, 1, 0, 0);
                AAAARawBufferClear.DispatchClear(context.cmd, _rawBufferClearCS, data.ActiveClustersMaskBuffer, data.ActiveClustersMaskBufferCount, 0, 0);
            }

            using (new ProfilingScope(context.cmd, Profiling.BuildClusterGrid))
            {
                const int kernelIndex = 0;
                const int threadGroupSize = AAAAClusteredLightingComputeShaders.BuildClusterGridThreadGroupSize;

                context.cmd.SetComputeBufferParam(_buildClusterGridCS, kernelIndex, ShaderIDs._ClusterBounds, data.ClusterBoundsBuffer);
                context.cmd.SetComputeVectorParam(_buildClusterGridCS, ShaderIDs.BuildClusterGrid._TileSizeInPixels, data.TileSizeInPixels);

                context.cmd.DispatchCompute(_buildClusterGridCS, kernelIndex,
                    AAAAMathUtils.AlignUp(TotalClusters, threadGroupSize) / threadGroupSize, 1, 1
                );
            }

            using (new ProfilingScope(context.cmd, Profiling.FindActiveClusters))
            {
                const int kernelIndex = 0;
                const int threadGroupSize = AAAAClusteredLightingComputeShaders.FindActiveClustersThreadGroupSize;

                context.cmd.SetComputeBufferParam(_findActiveClustersCS, kernelIndex, ShaderIDs._ActiveClustersMask, data.ActiveClustersMaskBuffer);

                int threadGroups = AAAAMathUtils.AlignUp(data.ScaledResolution.x * data.ScaledResolution.y, threadGroupSize) / threadGroupSize;
                context.cmd.DispatchCompute(_findActiveClustersCS, kernelIndex,
                    threadGroups, 1, 1
                );
            }

            using (new ProfilingScope(context.cmd, Profiling.CompactActiveClusterList))
            {
                const int kernelIndex = 0;
                const int threadGroupSize = AAAAClusteredLightingComputeShaders.CompactActiveClusterListThreadGroupSize;

                context.cmd.SetComputeBufferParam(_compactActiveClusterListCS, kernelIndex, ShaderIDs._ActiveClustersMask, data.ActiveClustersMaskBuffer);
                context.cmd.SetComputeBufferParam(_compactActiveClusterListCS, kernelIndex, ShaderIDs._ActiveClustersCount, data.ActiveClustersCountBuffer);
                context.cmd.SetComputeBufferParam(_compactActiveClusterListCS, kernelIndex, ShaderIDs._ActiveClustersList, data.ActiveClustersListBuffer);

                Assert.IsTrue(TotalClusters % threadGroupSize == 0);
                context.cmd.DispatchCompute(_compactActiveClusterListCS, kernelIndex,
                    TotalClusters / threadGroupSize, 1, 1
                );
            }

            using (new ProfilingScope(context.cmd, Profiling.FixupClusterCullingIndirectDispatchArgs))
            {
                const int kernelIndex = 0;

                context.cmd.SetComputeBufferParam(_fixupClusterCullingIndirectDispatchArgsCS, kernelIndex, ShaderIDs._ActiveClustersCount,
                    data.ActiveClustersCountBuffer
                );
                context.cmd.SetComputeBufferParam(_fixupClusterCullingIndirectDispatchArgsCS, kernelIndex,
                    ShaderIDs.FixupClusterCullingIndirectDispatchArgs._IndirectArgs, data.ClusterCullingIndirectArgsBuffer
                );

                context.cmd.DispatchCompute(_fixupClusterCullingIndirectDispatchArgsCS, kernelIndex, 1, 1, 1);
            }

            using (new ProfilingScope(context.cmd, Profiling.ClusterCulling))
            {
                const int kernelIndex = 0;

                context.cmd.SetComputeBufferParam(_clusterCullingCS, kernelIndex, ShaderIDs._ClusterBounds, data.ClusterBoundsBuffer);
                context.cmd.SetComputeBufferParam(_clusterCullingCS, kernelIndex, ShaderIDs.ClusterCulling._LightIndexCounter, data.LightIndexCounterBuffer);
                context.cmd.SetComputeBufferParam(_clusterCullingCS, kernelIndex, ShaderIDs.ClusterCulling._LightGrid, data.LightGridBuffer);
                context.cmd.SetComputeBufferParam(_clusterCullingCS, kernelIndex, ShaderIDs.ClusterCulling._LightIndexList, data.LightIndexListBuffer);
                context.cmd.SetComputeBufferParam(_clusterCullingCS, kernelIndex, ShaderIDs._ActiveClustersCount, data.ActiveClustersCountBuffer);
                context.cmd.SetComputeBufferParam(_clusterCullingCS, kernelIndex, ShaderIDs._ActiveClustersList, data.ActiveClustersListBuffer);

                context.cmd.DispatchCompute(_clusterCullingCS, kernelIndex, data.ClusterCullingIndirectArgsBuffer, 0);
            }

            context.cmd.SetGlobalBuffer(ShaderIDs.Global._ClusteredLightGrid, data.LightGridBuffer);
            context.cmd.SetGlobalBuffer(ShaderIDs.Global._ClusteredLightIndexList, data.LightIndexListBuffer);
        }

        public class PassData : PassDataBase
        {
            public BufferHandle ActiveClustersCountBuffer;
            public BufferHandle ActiveClustersListBuffer;
            public BufferHandle ActiveClustersMaskBuffer;
            public int ActiveClustersMaskBufferCount;
            public BufferHandle ClusterBoundsBuffer;
            public BufferHandle ClusterCullingIndirectArgsBuffer;
            public BufferHandle LightGridBuffer;
            public BufferHandle LightIndexCounterBuffer;
            public BufferHandle LightIndexListBuffer;
            public int2 ScaledResolution;
            public Vector4 TileSizeInPixels;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static readonly int _ClusterBounds = Shader.PropertyToID(nameof(_ClusterBounds));
            public static readonly int _ActiveClustersMask = Shader.PropertyToID(nameof(_ActiveClustersMask));
            public static readonly int _ActiveClustersCount = Shader.PropertyToID(nameof(_ActiveClustersCount));
            public static readonly int _ActiveClustersList = Shader.PropertyToID(nameof(_ActiveClustersList));

            public static class BuildClusterGrid
            {
                public static readonly int _TileSizeInPixels = Shader.PropertyToID(nameof(_TileSizeInPixels));
            }

            public static class FixupClusterCullingIndirectDispatchArgs
            {
                public static readonly int _IndirectArgs = Shader.PropertyToID(nameof(_IndirectArgs));
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
            public static readonly ProfilingSampler Prepare = new(nameof(Prepare));
            public static readonly ProfilingSampler BuildClusterGrid = new(nameof(BuildClusterGrid));
            public static readonly ProfilingSampler FindActiveClusters = new(nameof(FindActiveClusters));
            public static readonly ProfilingSampler CompactActiveClusterList = new(nameof(CompactActiveClusterList));
            public static readonly ProfilingSampler FixupClusterCullingIndirectDispatchArgs = new(nameof(FixupClusterCullingIndirectDispatchArgs));
            public static readonly ProfilingSampler ClusterCulling = new(nameof(ClusterCulling));
        }
    }
}