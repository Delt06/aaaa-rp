using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Meshlets;
using DELTation.AAAARP.Utils;
using JetBrains.Annotations;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes
{
    public class GPUMeshletCullingPass : AAAARenderPass<GPUMeshletCullingPass.PassData>
    {
        private readonly ComputeShader _fixupGPUMeshletCullingIndirectDispatchArgsCS;
        private readonly ComputeShader _fixupMeshletIndirectDrawArgsCS;
        private readonly ComputeShader _gpuMeshletCullingCS;
        private readonly ComputeShader _meshletListBuildCS;
        private readonly ComputeShader _meshletListBuildInitCS;
        private readonly ComputeShader _meshletListBuildSyncCS;
        private readonly ComputeShader _rawBufferClearCS;

        public GPUMeshletCullingPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders runtimeShaders) : base(renderPassEvent)
        {
            _rawBufferClearCS = runtimeShaders.RawBufferClearCS;
            _meshletListBuildInitCS = runtimeShaders.MeshletListBuildInitCS;
            _meshletListBuildCS = runtimeShaders.MeshletListBuildCS;
            _meshletListBuildSyncCS = runtimeShaders.MeshletListBuildSyncCS;
            _fixupGPUMeshletCullingIndirectDispatchArgsCS = runtimeShaders.FixupGPUMeshletCullingIndirectDispatchArgsCS;
            _gpuMeshletCullingCS = runtimeShaders.GPUMeshletCullingCS;
            _fixupMeshletIndirectDrawArgsCS = runtimeShaders.FixupMeshletIndirectDrawArgsCS;
        }

        [CanBeNull]
        public Camera CullingCameraOverride { get; set; }

        public override string Name => "GPUMeshletCulling";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAAVisibilityBufferContainer visibilityBufferContainer = renderingData.VisibilityBufferContainer;
            passData.InstanceCount = visibilityBufferContainer.InstanceCount;
            if (passData.InstanceCount == 0)
            {
                return;
            }

            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            Camera camera = CullingCameraOverride != null ? CullingCameraOverride : cameraData.Camera;
            Vector3 cameraPosition = camera.transform.position;
            passData.CameraPosition = new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 1);
            Plane[] frustumPlanes = TempCollections.Planes;
            GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);

            for (int i = 0; i < frustumPlanes.Length; i++)
            {
                Plane frustumPlane = frustumPlanes[i];
                passData.FrustumPlanes[i] = new Vector4(frustumPlane.normal.x, frustumPlane.normal.y, frustumPlane.normal.z, frustumPlane.distance);
            }

            passData.CameraViewProjectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix * camera.worldToCameraMatrix, true);

            GraphicsBuffer meshletRenderRequestsBuffer = visibilityBufferContainer.MeshletRenderRequestsBuffer;

            passData.InitialMeshletListCounterBuffer = builder.CreateTransientBuffer(CreateCounterBufferDesc("InitialMeshletListCounter"));
            passData.InitialMeshletListBuffer = builder.CreateTransientBuffer(
                new BufferDesc(meshletRenderRequestsBuffer.count, meshletRenderRequestsBuffer.stride, meshletRenderRequestsBuffer.target)
                {
                    name = "InitialMeshletList",
                }
            );
            passData.CullingIndirectDispatchArgsBuffer = builder.CreateTransientBuffer(
                new BufferDesc(1, UnsafeUtility.SizeOf<IndirectDispatchArgs>(), GraphicsBuffer.Target.IndirectArguments)
                {
                    name = "GPUMeshletCullingIndirectDispatchArgs",
                }
            );

            passData.MeshletListBuildIndirectDispatchArgsBuffer = builder.CreateTransientBuffer(
                new BufferDesc(1, UnsafeUtility.SizeOf<IndirectDispatchArgs>(), GraphicsBuffer.Target.IndirectArguments)
                {
                    name = "MeshletListBuildIndirectDispatchArgs",
                }
            );

            passData.MeshletListBuildWorkQueueCapacity = visibilityBufferContainer.MaxMeshLODNodesPerLevel * 4;
            passData.MeshletListBuildWorkQueue0 = builder.CreateTransientBuffer(
                new BufferDesc(passData.MeshletListBuildWorkQueueCapacity, sizeof(uint), GraphicsBuffer.Target.Raw)
                {
                    name = nameof(PassData.MeshletListBuildWorkQueue0),
                }
            );
            passData.MeshletListBuildWorkQueue1 = builder.CreateTransientBuffer(
                new BufferDesc(passData.MeshletListBuildWorkQueueCapacity, sizeof(uint), GraphicsBuffer.Target.Raw)
                {
                    name = nameof(PassData.MeshletListBuildWorkQueue1),
                }
            );
            passData.MeshletListBuildWorkQueueSize0 = builder.CreateTransientBuffer(
                new BufferDesc(1, sizeof(uint), GraphicsBuffer.Target.Raw)
                {
                    name = nameof(PassData.MeshletListBuildWorkQueueSize0),
                }
            );
            passData.MeshletListBuildWorkQueueSize1 = builder.CreateTransientBuffer(
                new BufferDesc(1, sizeof(uint), GraphicsBuffer.Target.Raw)
                {
                    name = nameof(PassData.MeshletListBuildWorkQueueSize1),
                }
            );
            passData.VisitedMaskCapacity = visibilityBufferContainer.VisitedMaskCapacity;
            passData.VisitedMask = builder.CreateTransientBuffer(
                new BufferDesc(passData.VisitedMaskCapacity, sizeof(uint), GraphicsBuffer.Target.Raw)
                {
                    name = nameof(PassData.VisitedMask),
                }
            );
            passData.VisitedMaskAllocator = builder.CreateTransientBuffer(
                new BufferDesc(1, sizeof(uint), GraphicsBuffer.Target.Raw)
                {
                    name = nameof(PassData.VisitedMaskAllocator),
                }
            );
            passData.MeshletListBuildDepth = Mathf.CeilToInt(0.5f * visibilityBufferContainer.MaxMeshLODLevelsCount);

            passData.DestinationMeshletsCounterBuffer = builder.CreateTransientBuffer(CreateCounterBufferDesc("MeshletRenderRequestCounter"));
            passData.DestinationMeshletsBuffer = renderingData.RenderGraph.ImportBuffer(meshletRenderRequestsBuffer);
            builder.WriteBuffer(passData.DestinationMeshletsBuffer);

            passData.IndirectDrawArgsBuffer = renderingData.RenderGraph.ImportBuffer(visibilityBufferContainer.IndirectDrawArgsBuffer);
            builder.WriteBuffer(passData.IndirectDrawArgsBuffer);
        }

        private static BufferDesc CreateCounterBufferDesc(string name) =>
            new(1, sizeof(uint), GraphicsBuffer.Target.Raw)
            {
                name = name,
            };

        protected override void Render(PassData data, RenderGraphContext context)
        {
            if (data.InstanceCount == 0)
            {
                return;
            }

            using (new ProfilingScope(context.cmd, Profiling.ClearRenderRequestsCount))
            {
                AAAARawBufferClear.DispatchClear(context.cmd, _rawBufferClearCS, data.InitialMeshletListCounterBuffer, 1, 0, 0);
            }

            using (new ProfilingScope(context.cmd, Profiling.MeshletListBuildHierarchical))
            {
                var source = new WorkQueueBuffer(data.MeshletListBuildWorkQueue0, data.MeshletListBuildWorkQueueSize0);
                var destination = new WorkQueueBuffer(data.MeshletListBuildWorkQueue1, data.MeshletListBuildWorkQueueSize1);

                using (new ProfilingScope(context.cmd, Profiling.MeshletListBuildInit))
                {
                    AAAARawBufferClear.DispatchClear(context.cmd, _rawBufferClearCS, data.VisitedMask, data.VisitedMaskCapacity, 0, 0);
                    AAAARawBufferClear.DispatchClear(context.cmd, _rawBufferClearCS, data.VisitedMaskAllocator, 1, 0, 0);
                    AAAARawBufferClear.DispatchClear(context.cmd, _rawBufferClearCS, data.MeshletListBuildWorkQueueSize0, 1, 0, 0);
                    AAAARawBufferClear.DispatchClear(context.cmd, _rawBufferClearCS, data.MeshletListBuildWorkQueueSize1, 1, 0, 0);

                    const int kernelIndex = 0;

                    context.cmd.SetComputeVectorArrayParam(_meshletListBuildInitCS, ShaderID.MeshletListBuild_Init._CameraFrustumPlanes, data.FrustumPlanes);
                    context.cmd.SetComputeMatrixParam(_meshletListBuildInitCS, ShaderID.MeshletListBuild_Init._CameraViewProjection,
                        data.CameraViewProjectionMatrix
                    );

                    context.cmd.SetComputeBufferParam(_meshletListBuildInitCS, kernelIndex, ShaderID.MeshletListBuild_Init._WorkQueue, source.Queue);
                    context.cmd.SetComputeBufferParam(_meshletListBuildInitCS, kernelIndex, ShaderID.MeshletListBuild_Init._WorkQueueSize, source.Size);
                    context.cmd.SetComputeBufferParam(_meshletListBuildInitCS, kernelIndex, ShaderID.MeshletListBuild_Init._VisitedMaskAllocator,
                        data.VisitedMaskAllocator
                    );

                    const int groupSize = (int) AAAAMeshletComputeShaders.MeshletListBuildInitThreadGroupSize;
                    context.cmd.DispatchCompute(_meshletListBuildInitCS, kernelIndex,
                        AAAAMathUtils.AlignUp(data.InstanceCount, groupSize) / groupSize, 1, 1
                    );
                }

                for (int i = 0; i < data.MeshletListBuildDepth; ++i)
                {
                    using (new ProfilingScope(context.cmd, Profiling.MeshletListBuildSync))
                    {
                        const int kernelIndex = 0;

                        context.cmd.SetComputeBufferParam(_meshletListBuildSyncCS, kernelIndex,
                            ShaderID.MeshletListBuild_Sync._ResetWorkQueueSize, destination.Size
                        );
                        context.cmd.SetComputeBufferParam(_meshletListBuildSyncCS, kernelIndex,
                            ShaderID.MeshletListBuild_Sync._FixupWorkQueueSize, source.Size
                        );
                        context.cmd.SetComputeBufferParam(_meshletListBuildSyncCS, kernelIndex,
                            ShaderID.MeshletListBuild_Sync._IndirectArgs, data.MeshletListBuildIndirectDispatchArgsBuffer
                        );
                        context.cmd.DispatchCompute(_meshletListBuildSyncCS, kernelIndex, 1, 1, 1);
                    }

                    using (new ProfilingScope(context.cmd, Profiling.MeshletListBuild))
                    {
                        const int kernelIndex = 0;

                        context.cmd.SetComputeBufferParam(_meshletListBuildCS, kernelIndex,
                            ShaderID.MeshletListBuild._SourceWorkQueue, source.Queue
                        );
                        context.cmd.SetComputeBufferParam(_meshletListBuildCS, kernelIndex,
                            ShaderID.MeshletListBuild._SourceWorkQueueSize, source.Size
                        );
                        context.cmd.SetComputeBufferParam(_meshletListBuildCS, kernelIndex,
                            ShaderID.MeshletListBuild._DestinationWorkQueue, destination.Queue
                        );
                        context.cmd.SetComputeBufferParam(_meshletListBuildCS, kernelIndex,
                            ShaderID.MeshletListBuild._DestinationWorkQueueSize, destination.Size
                        );
                        context.cmd.SetComputeBufferParam(_meshletListBuildCS, kernelIndex,
                            ShaderID.MeshletListBuild._VisitedMask, data.VisitedMask
                        );

                        context.cmd.SetComputeBufferParam(_meshletListBuildCS, kernelIndex,
                            ShaderID.MeshletListBuild._DestinationMeshletsCounter, data.InitialMeshletListCounterBuffer
                        );
                        context.cmd.SetComputeBufferParam(_meshletListBuildCS, kernelIndex,
                            ShaderID.MeshletListBuild._DestinationMeshlets, data.InitialMeshletListBuffer
                        );

                        context.cmd.DispatchCompute(_meshletListBuildCS, kernelIndex,
                            data.MeshletListBuildIndirectDispatchArgsBuffer, 0
                        );
                    }

                    (source, destination) = (destination, source);
                }
            }

            using (new ProfilingScope(context.cmd, Profiling.FixupMeshletCullingIndirectDispatchArgs))
            {
                const int kernelIndex = 0;

                context.cmd.SetComputeBufferParam(_fixupGPUMeshletCullingIndirectDispatchArgsCS, kernelIndex,
                    ShaderID.FixupMeshletCullingIndirectDispatchArgs._RequestCounter, data.InitialMeshletListCounterBuffer
                );
                context.cmd.SetComputeBufferParam(_fixupGPUMeshletCullingIndirectDispatchArgsCS, kernelIndex,
                    ShaderID.FixupMeshletCullingIndirectDispatchArgs._IndirectArgs, data.CullingIndirectDispatchArgsBuffer
                );
                context.cmd.DispatchCompute(_fixupGPUMeshletCullingIndirectDispatchArgsCS, kernelIndex, 1, 1, 1);
            }

            using (new ProfilingScope(context.cmd, Profiling.ClearRenderRequestsCount))
            {
                AAAARawBufferClear.DispatchClear(context.cmd, _rawBufferClearCS, data.DestinationMeshletsCounterBuffer, 1, 0, 0);
            }

            using (new ProfilingScope(context.cmd, Profiling.MeshletCulling))
            {
                const int kernelIndex = 0;

                context.cmd.SetComputeVectorArrayParam(_gpuMeshletCullingCS, ShaderID.MeshletCulling._CameraFrustumPlanes, data.FrustumPlanes);
                context.cmd.SetComputeVectorParam(_gpuMeshletCullingCS, ShaderID.MeshletCulling._CameraPosition, data.CameraPosition);

                context.cmd.SetComputeBufferParam(_gpuMeshletCullingCS, kernelIndex,
                    ShaderID.MeshletCulling._SourceMeshletsCounter, data.InitialMeshletListCounterBuffer
                );
                context.cmd.SetComputeBufferParam(_gpuMeshletCullingCS, kernelIndex,
                    ShaderID.MeshletCulling._SourceMeshlets, data.InitialMeshletListBuffer
                );

                context.cmd.SetComputeBufferParam(_gpuMeshletCullingCS, kernelIndex,
                    ShaderID.MeshletCulling._DestinationMeshletsCounter, data.DestinationMeshletsCounterBuffer
                );
                context.cmd.SetComputeBufferParam(_gpuMeshletCullingCS, kernelIndex,
                    ShaderID.MeshletCulling._DestinationMeshlets, data.DestinationMeshletsBuffer
                );

                context.cmd.DispatchCompute(_gpuMeshletCullingCS, kernelIndex, data.CullingIndirectDispatchArgsBuffer, 0);
            }

            using (new ProfilingScope(context.cmd, Profiling.FixupIndirectDrawArgs))
            {
                const int kernelIndex = 0;

                context.cmd.SetComputeBufferParam(_fixupMeshletIndirectDrawArgsCS, kernelIndex,
                    ShaderID.FixupIndirectDrawArgs._RequestCounter, data.DestinationMeshletsCounterBuffer
                );
                context.cmd.SetComputeBufferParam(_fixupMeshletIndirectDrawArgsCS, kernelIndex,
                    ShaderID.FixupIndirectDrawArgs._IndirectArgs, data.IndirectDrawArgsBuffer
                );
                context.cmd.DispatchCompute(_fixupMeshletIndirectDrawArgsCS, kernelIndex, 1, 1, 1);
            }
        }

        private struct WorkQueueBuffer
        {
            public readonly BufferHandle Queue;
            public readonly BufferHandle Size;

            public WorkQueueBuffer(BufferHandle queue, BufferHandle size)
            {
                Queue = queue;
                Size = size;
            }
        }

        public class PassData : PassDataBase
        {
            public readonly Vector4[] FrustumPlanes = new Vector4[6];
            public Vector4 CameraPosition;
            public Matrix4x4 CameraViewProjectionMatrix;
            public BufferHandle CullingIndirectDispatchArgsBuffer;

            public BufferHandle DestinationMeshletsBuffer;
            public BufferHandle DestinationMeshletsCounterBuffer;
            public BufferHandle IndirectDrawArgsBuffer;

            public BufferHandle InitialMeshletListBuffer;
            public BufferHandle InitialMeshletListCounterBuffer;

            public int InstanceCount;
            public int MeshletListBuildDepth;

            public BufferHandle MeshletListBuildIndirectDispatchArgsBuffer;
            public BufferHandle MeshletListBuildWorkQueue0;
            public BufferHandle MeshletListBuildWorkQueue1;

            public int MeshletListBuildWorkQueueCapacity;
            public BufferHandle MeshletListBuildWorkQueueSize0;
            public BufferHandle MeshletListBuildWorkQueueSize1;
            public BufferHandle VisitedMask;
            public BufferHandle VisitedMaskAllocator;

            public int VisitedMaskCapacity;
        }

        private static class Profiling
        {
            public static readonly ProfilingSampler ClearRenderRequestsCount = new(nameof(ClearRenderRequestsCount));
            public static readonly ProfilingSampler MeshletListBuildHierarchical = new(nameof(MeshletListBuildHierarchical));
            public static readonly ProfilingSampler MeshletListBuildInit = new(nameof(MeshletListBuildInit));
            public static readonly ProfilingSampler MeshletListBuild = new(nameof(MeshletListBuild));
            public static readonly ProfilingSampler MeshletListBuildSync = new(nameof(MeshletListBuildSync));
            public static readonly ProfilingSampler FixupMeshletCullingIndirectDispatchArgs = new(nameof(FixupMeshletCullingIndirectDispatchArgs));
            public static readonly ProfilingSampler MeshletCulling = new(nameof(MeshletCulling));
            public static readonly ProfilingSampler FixupIndirectDrawArgs = new(nameof(FixupIndirectDrawArgs));
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderID
        {
            public static class MeshletListBuild_Init
            {
                public static int _CameraFrustumPlanes = Shader.PropertyToID(nameof(_CameraFrustumPlanes));
                public static int _CameraViewProjection = Shader.PropertyToID(nameof(_CameraViewProjection));

                public static int _WorkQueue = Shader.PropertyToID(nameof(_WorkQueue));
                public static int _WorkQueueSize = Shader.PropertyToID(nameof(_WorkQueueSize));
                public static int _VisitedMaskAllocator = Shader.PropertyToID(nameof(_VisitedMaskAllocator));
            }

            public static class MeshletListBuild
            {
                public static int _SourceWorkQueue = Shader.PropertyToID(nameof(_SourceWorkQueue));
                public static int _SourceWorkQueueSize = Shader.PropertyToID(nameof(_SourceWorkQueueSize));
                public static int _DestinationWorkQueue = Shader.PropertyToID(nameof(_DestinationWorkQueue));
                public static int _DestinationWorkQueueSize = Shader.PropertyToID(nameof(_DestinationWorkQueueSize));
                public static int _VisitedMask = Shader.PropertyToID(nameof(_VisitedMask));

                public static int _DestinationMeshletsCounter = Shader.PropertyToID(nameof(_DestinationMeshletsCounter));
                public static int _DestinationMeshlets = Shader.PropertyToID(nameof(_DestinationMeshlets));
            }

            public static class MeshletListBuild_Sync
            {
                public static int _ResetWorkQueueSize = Shader.PropertyToID(nameof(_ResetWorkQueueSize));
                public static int _FixupWorkQueueSize = Shader.PropertyToID(nameof(_FixupWorkQueueSize));
                public static int _IndirectArgs = Shader.PropertyToID(nameof(_IndirectArgs));
            }

            public static class FixupMeshletCullingIndirectDispatchArgs
            {
                public static int _RequestCounter = Shader.PropertyToID(nameof(_RequestCounter));
                public static int _IndirectArgs = Shader.PropertyToID(nameof(_IndirectArgs));
            }

            public static class MeshletCulling
            {
                public static int _CameraFrustumPlanes = Shader.PropertyToID(nameof(_CameraFrustumPlanes));
                public static int _CameraPosition = Shader.PropertyToID(nameof(_CameraPosition));

                public static int _SourceMeshletsCounter = Shader.PropertyToID(nameof(_SourceMeshletsCounter));
                public static int _SourceMeshlets = Shader.PropertyToID(nameof(_SourceMeshlets));

                public static int _DestinationMeshletsCounter = Shader.PropertyToID(nameof(_DestinationMeshletsCounter));
                public static int _DestinationMeshlets = Shader.PropertyToID(nameof(_DestinationMeshlets));
            }

            public static class FixupIndirectDrawArgs
            {
                public static int _RequestCounter = Shader.PropertyToID(nameof(_RequestCounter));
                public static int _IndirectArgs = Shader.PropertyToID(nameof(_IndirectArgs));
            }
        }
    }
}