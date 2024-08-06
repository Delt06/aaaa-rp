using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.FrameData;
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
        private readonly ComputeShader _rawBufferClearCS;

        public GPUMeshletCullingPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders runtimeShaders) : base(renderPassEvent)
        {
            _rawBufferClearCS = runtimeShaders.RawBufferClearCS;
            _meshletListBuildCS = runtimeShaders.MeshletListBuildCS;
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

            using (new ProfilingScope(context.cmd, Profiling.MeshletListBuild))
            {
                const int kernelIndex = 0;

                context.cmd.SetComputeVectorArrayParam(_meshletListBuildCS, ShaderID.MeshletListBuild._CameraFrustumPlanes, data.FrustumPlanes);
                context.cmd.SetComputeMatrixParam(_meshletListBuildCS, ShaderID.MeshletListBuild._CameraViewProjection, data.CameraViewProjectionMatrix);

                context.cmd.SetComputeBufferParam(_meshletListBuildCS, kernelIndex,
                    ShaderID.MeshletListBuild._DestinationMeshletsCounter, data.InitialMeshletListCounterBuffer
                );
                context.cmd.SetComputeBufferParam(_meshletListBuildCS, kernelIndex,
                    ShaderID.MeshletListBuild._DestinationMeshlets, data.InitialMeshletListBuffer
                );

                context.cmd.DispatchCompute(_meshletListBuildCS, kernelIndex,
                    data.InstanceCount, 1, 1
                );
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
        }

        private static class Profiling
        {
            public static readonly ProfilingSampler ClearRenderRequestsCount = new(nameof(ClearRenderRequestsCount));
            public static readonly ProfilingSampler MeshletListBuild = new(nameof(MeshletListBuild));
            public static readonly ProfilingSampler FixupMeshletCullingIndirectDispatchArgs = new(nameof(FixupMeshletCullingIndirectDispatchArgs));
            public static readonly ProfilingSampler MeshletCulling = new(nameof(MeshletCulling));
            public static readonly ProfilingSampler FixupIndirectDrawArgs = new(nameof(FixupIndirectDrawArgs));
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderID
        {
            public static class MeshletListBuild
            {
                public static int _CameraFrustumPlanes = Shader.PropertyToID(nameof(_CameraFrustumPlanes));
                public static int _CameraViewProjection = Shader.PropertyToID(nameof(_CameraViewProjection));

                public static int _DestinationMeshletsCounter = Shader.PropertyToID(nameof(_DestinationMeshletsCounter));
                public static int _DestinationMeshlets = Shader.PropertyToID(nameof(_DestinationMeshlets));
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