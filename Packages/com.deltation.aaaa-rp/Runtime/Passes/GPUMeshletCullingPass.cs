﻿using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Meshlets;
using DELTation.AAAARP.Renderers;
using DELTation.AAAARP.Utils;
using JetBrains.Annotations;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes
{
    public class GPUMeshletCullingPass : AAAARenderPass<GPUMeshletCullingPass.PassData>
    {
        private readonly ComputeShader _fixupGPUMeshletCullingIndirectDispatchArgsCS;
        private readonly ComputeShader _fixupMeshletIndirectDrawArgsCS;
        private readonly ComputeShader _fixupMeshletListBuildIndirectDispatchArgsCS;
        private readonly ComputeShader _gpuInstanceCullingCS;
        private readonly ComputeShader _gpuMeshletCullingCS;
        private readonly ComputeShader _meshletListBuildCS;
        private readonly ComputeShader _rawBufferClearCS;

        public GPUMeshletCullingPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders runtimeShaders) : base(renderPassEvent)
        {
            _rawBufferClearCS = runtimeShaders.RawBufferClearCS;
            _gpuInstanceCullingCS = runtimeShaders.GPUInstanceCullingCS;
            _fixupMeshletListBuildIndirectDispatchArgsCS = runtimeShaders.FixupMeshletListBuildIndirectDispatchArgsCS;
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
            AAAARendererContainer rendererContainer = renderingData.RendererContainer;
            passData.InstanceCount = rendererContainer.InstanceCount;
            if (passData.InstanceCount == 0)
            {
                return;
            }

            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            Camera camera = CullingCameraOverride != null ? CullingCameraOverride : cameraData.Camera;
            Transform cameraTransform = camera.transform;
            Vector3 cameraPosition = cameraTransform.position;
            passData.CameraPosition = new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 1);
            passData.CameraUp = cameraTransform.up;
            passData.CameraRight = cameraTransform.right;
            passData.ScreenSizePixels = new Vector2(cameraData.ScaledWidth, cameraData.ScaledHeight);
            passData.CotHalfFov = 1.0f / math.tan(0.5f * Mathf.Deg2Rad * cameraData.Camera.fieldOfView);

            Plane[] frustumPlanes = TempCollections.Planes;
            GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);

            for (int i = 0; i < frustumPlanes.Length; i++)
            {
                Plane frustumPlane = frustumPlanes[i];
                passData.FrustumPlanes[i] = new Vector4(frustumPlane.normal.x, frustumPlane.normal.y, frustumPlane.normal.z, frustumPlane.distance);
            }

            passData.CameraViewProjectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix * camera.worldToCameraMatrix, true);

            GraphicsBuffer meshletRenderRequestsBuffer = rendererContainer.MeshletRenderRequestsBuffer;

            passData.InitialMeshletListCounterBuffer = builder.CreateTransientBuffer(CreateCounterBufferDesc("InitialMeshletListCounter"));
            passData.InitialMeshletListBuffer = builder.CreateTransientBuffer(
                new BufferDesc(meshletRenderRequestsBuffer.count, meshletRenderRequestsBuffer.stride, meshletRenderRequestsBuffer.target)
                {
                    name = nameof(PassData.InitialMeshletListBuffer),
                }
            );
            passData.GPUMeshletCullingIndirectDispatchArgsBuffer = builder.CreateTransientBuffer(
                new BufferDesc(1, UnsafeUtility.SizeOf<IndirectDispatchArgs>(), GraphicsBuffer.Target.IndirectArguments)
                {
                    name = nameof(PassData.GPUMeshletCullingIndirectDispatchArgsBuffer),
                }
            );

            passData.MeshletListBuildIndirectDispatchArgsBuffer = builder.CreateTransientBuffer(
                new BufferDesc(1, UnsafeUtility.SizeOf<IndirectDispatchArgs>(), GraphicsBuffer.Target.IndirectArguments)
                {
                    name = "MeshletListBuildIndirectDispatchArgs",
                }
            );
            passData.MeshletListBuildJobCounterBuffer = builder.CreateTransientBuffer(
                new BufferDesc(1, sizeof(uint), GraphicsBuffer.Target.Raw)
                {
                    name = nameof(PassData.MeshletListBuildJobCounterBuffer),
                }
            );
            passData.MeshletListBuildJobsBuffer = builder.CreateTransientBuffer(
                new BufferDesc(rendererContainer.MaxMeshletListBuildJobCount, UnsafeUtility.SizeOf<AAAAMeshletListBuildJob>(),
                    GraphicsBuffer.Target.Structured
                )
                {
                    name = nameof(PassData.MeshletListBuildJobsBuffer),
                }
            );

            passData.DestinationMeshletsCounterBuffer = builder.CreateTransientBuffer(CreateCounterBufferDesc("MeshletRenderRequestCounter"));
            passData.DestinationMeshletsBuffer = renderingData.RenderGraph.ImportBuffer(meshletRenderRequestsBuffer);
            builder.WriteBuffer(passData.DestinationMeshletsBuffer);

            passData.IndirectDrawArgsBuffer = renderingData.RenderGraph.ImportBuffer(rendererContainer.IndirectDrawArgsBuffer);
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

            using (new ProfilingScope(context.cmd, Profiling.ClearBuffers))
            {
                AAAARawBufferClear.DispatchClear(context.cmd, _rawBufferClearCS, data.InitialMeshletListCounterBuffer, 1, 0, 0);
                AAAARawBufferClear.DispatchClear(context.cmd, _rawBufferClearCS, data.MeshletListBuildJobCounterBuffer, 1, 0, 0);
            }

            using (new ProfilingScope(context.cmd, Profiling.InstanceCulling))
            {
                const int kernelIndex = 0;

                context.cmd.SetComputeVectorArrayParam(_gpuInstanceCullingCS, ShaderID.GPUInstanceCulling._CameraFrustumPlanes, data.FrustumPlanes);
                context.cmd.SetComputeMatrixParam(_gpuInstanceCullingCS, ShaderID.GPUInstanceCulling._CameraViewProjection, data.CameraViewProjectionMatrix);

                context.cmd.SetComputeBufferParam(_gpuInstanceCullingCS, kernelIndex,
                    ShaderID.GPUInstanceCulling._Jobs, data.MeshletListBuildJobsBuffer
                );
                context.cmd.SetComputeBufferParam(_gpuInstanceCullingCS, kernelIndex,
                    ShaderID.GPUInstanceCulling._JobCounter, data.MeshletListBuildJobCounterBuffer
                );

                const int groupSize = (int) AAAAMeshletComputeShaders.GPUInstanceCullingThreadGroupSize;
                context.cmd.DispatchCompute(_gpuInstanceCullingCS, kernelIndex,
                    AAAAMathUtils.AlignUp(data.InstanceCount, groupSize) / groupSize, 1, 1
                );
            }

            using (new ProfilingScope(context.cmd, Profiling.FixupMeshletListBuildIndirectDispatchArgs))
            {
                const int kernelIndex = 0;

                context.cmd.SetComputeBufferParam(_fixupMeshletListBuildIndirectDispatchArgsCS, kernelIndex,
                    ShaderID.FixupMeshletListBuildIndirectDispatchArgs._JobCounter, data.MeshletListBuildJobCounterBuffer
                );
                context.cmd.SetComputeBufferParam(_fixupMeshletListBuildIndirectDispatchArgsCS, kernelIndex,
                    ShaderID.FixupMeshletListBuildIndirectDispatchArgs._IndirectArgs, data.MeshletListBuildIndirectDispatchArgsBuffer
                );
                context.cmd.DispatchCompute(_fixupMeshletListBuildIndirectDispatchArgsCS, kernelIndex, 1, 1, 1);
            }

            using (new ProfilingScope(context.cmd, Profiling.MeshletListBuild))
            {
                const int kernelIndex = 0;

                context.cmd.SetComputeMatrixParam(_meshletListBuildCS, ShaderID.MeshletListBuild._CameraViewProjection, data.CameraViewProjectionMatrix);
                context.cmd.SetComputeVectorParam(_meshletListBuildCS, ShaderID.MeshletListBuild._CameraPosition, data.CameraPosition);
                context.cmd.SetComputeVectorParam(_meshletListBuildCS, ShaderID.MeshletListBuild._CameraUp, data.CameraUp);
                context.cmd.SetComputeVectorParam(_meshletListBuildCS, ShaderID.MeshletListBuild._CameraRight, data.CameraRight);
                context.cmd.SetComputeVectorParam(_meshletListBuildCS, ShaderID.MeshletListBuild._ScreenSizePixels, data.ScreenSizePixels);

                context.cmd.SetComputeBufferParam(_meshletListBuildCS, kernelIndex,
                    ShaderID.MeshletListBuild._Jobs, data.MeshletListBuildJobsBuffer
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

            using (new ProfilingScope(context.cmd, Profiling.FixupMeshletCullingIndirectDispatchArgs))
            {
                const int kernelIndex = 0;

                context.cmd.SetComputeBufferParam(_fixupGPUMeshletCullingIndirectDispatchArgsCS, kernelIndex,
                    ShaderID.FixupMeshletCullingIndirectDispatchArgs._RequestCounter, data.InitialMeshletListCounterBuffer
                );
                context.cmd.SetComputeBufferParam(_fixupGPUMeshletCullingIndirectDispatchArgsCS, kernelIndex,
                    ShaderID.FixupMeshletCullingIndirectDispatchArgs._IndirectArgs, data.GPUMeshletCullingIndirectDispatchArgsBuffer
                );
                context.cmd.DispatchCompute(_fixupGPUMeshletCullingIndirectDispatchArgsCS, kernelIndex, 1, 1, 1);
            }

            using (new ProfilingScope(context.cmd, Profiling.ClearBuffers))
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

                context.cmd.DispatchCompute(_gpuMeshletCullingCS, kernelIndex, data.GPUMeshletCullingIndirectDispatchArgsBuffer, 0);
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
            public Vector4 CameraRight;
            public Vector4 CameraUp;
            public Matrix4x4 CameraViewProjectionMatrix;
            public float CotHalfFov;

            public BufferHandle DestinationMeshletsBuffer;
            public BufferHandle DestinationMeshletsCounterBuffer;
            public BufferHandle GPUMeshletCullingIndirectDispatchArgsBuffer;
            public BufferHandle IndirectDrawArgsBuffer;

            public BufferHandle InitialMeshletListBuffer;
            public BufferHandle InitialMeshletListCounterBuffer;

            public int InstanceCount;

            public BufferHandle MeshletListBuildIndirectDispatchArgsBuffer;
            public BufferHandle MeshletListBuildJobCounterBuffer;
            public BufferHandle MeshletListBuildJobsBuffer;
            public Vector2 ScreenSizePixels;
        }

        private static class Profiling
        {
            public static readonly ProfilingSampler ClearBuffers = new(nameof(ClearBuffers));
            public static readonly ProfilingSampler InstanceCulling = new(nameof(InstanceCulling));
            public static readonly ProfilingSampler FixupMeshletListBuildIndirectDispatchArgs = new(nameof(FixupMeshletListBuildIndirectDispatchArgs));
            public static readonly ProfilingSampler MeshletListBuild = new(nameof(MeshletListBuild));
            public static readonly ProfilingSampler FixupMeshletCullingIndirectDispatchArgs = new(nameof(FixupMeshletCullingIndirectDispatchArgs));
            public static readonly ProfilingSampler MeshletCulling = new(nameof(MeshletCulling));
            public static readonly ProfilingSampler FixupIndirectDrawArgs = new(nameof(FixupIndirectDrawArgs));
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderID
        {
            public static class GPUInstanceCulling
            {
                public static int _CameraFrustumPlanes = Shader.PropertyToID(nameof(_CameraFrustumPlanes));
                public static int _CameraViewProjection = Shader.PropertyToID(nameof(_CameraViewProjection));

                public static int _Jobs = Shader.PropertyToID(nameof(_Jobs));
                public static int _JobCounter = Shader.PropertyToID(nameof(_JobCounter));
            }

            public static class FixupMeshletListBuildIndirectDispatchArgs
            {
                public static int _JobCounter = Shader.PropertyToID(nameof(_JobCounter));
                public static int _IndirectArgs = Shader.PropertyToID(nameof(_IndirectArgs));
            }

            public static class MeshletListBuild
            {
                public static int _CameraViewProjection = Shader.PropertyToID(nameof(_CameraViewProjection));
                public static int _CameraPosition = Shader.PropertyToID(nameof(_CameraPosition));
                public static int _CameraUp = Shader.PropertyToID(nameof(_CameraUp));
                public static int _CameraRight = Shader.PropertyToID(nameof(_CameraRight));
                public static int _ScreenSizePixels = Shader.PropertyToID(nameof(_ScreenSizePixels));

                public static int _Jobs = Shader.PropertyToID(nameof(_Jobs));

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