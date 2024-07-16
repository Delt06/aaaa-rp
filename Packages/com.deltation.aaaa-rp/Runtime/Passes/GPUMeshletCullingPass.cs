﻿using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Meshlets;
using DELTation.AAAARP.Utils;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes
{
    public class GPUMeshletCullingPassData : PassDataBase
    {
        public readonly Vector4[] FrustumPlanes = new Vector4[6];
        public Vector4 CameraPosition;
        public Matrix4x4 CameraViewProjectionMatrix;
        public BufferHandle DestinationMeshletsBuffer;
        public BufferHandle DestinationMeshletsCounterBuffer;
        public AAAAVisibilityBufferContainer VisibilityBufferContainer;
    }

    public class GPUMeshletCullingPass : AAAARenderPass<GPUMeshletCullingPassData>
    {
        private readonly ComputeShader _fixupMeshletIndirectDrawArgsCS;
        private readonly ComputeShader _gpuMeshletCullingCS;
        private readonly ComputeShader _rawBufferClearCS;

        public GPUMeshletCullingPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders runtimeShaders) : base(renderPassEvent)
        {
            _rawBufferClearCS = runtimeShaders.RawBufferClearCS;
            _gpuMeshletCullingCS = runtimeShaders.GPUMeshletCullingCS;
            _fixupMeshletIndirectDrawArgsCS = runtimeShaders.FixupMeshletIndirectDrawArgsCS;
        }

        [CanBeNull]
        public Camera CullingCameraOverride { get; set; }

        public override string Name => "GPUMeshletCulling";

        protected override void Setup(RenderGraphBuilder builder, GPUMeshletCullingPassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAAVisibilityBufferContainer visibilityBufferContainer = renderingData.VisibilityBufferContainer;
            passData.VisibilityBufferContainer = visibilityBufferContainer;

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

            passData.DestinationMeshletsCounterBuffer = builder.CreateTransientBuffer(new BufferDesc(1, sizeof(uint), GraphicsBuffer.Target.Raw)
                {
                    name = "MeshletRenderRequestCounter",
                }
            );
            passData.DestinationMeshletsBuffer = renderingData.RenderGraph.ImportBuffer(visibilityBufferContainer.MeshletRenderRequestsBuffer);
            builder.WriteBuffer(passData.DestinationMeshletsBuffer);
        }

        protected override void Render(GPUMeshletCullingPassData data, RenderGraphContext context)
        {
            int instanceCount = data.VisibilityBufferContainer.InstanceCount;
            if (instanceCount == 0)
            {
                return;
            }

            using (new ProfilingScope(context.cmd, Profiling.ClearRenderRequestsCount))
            {
                AAAARawBufferClear.DispatchClear(context.cmd, _rawBufferClearCS, data.DestinationMeshletsCounterBuffer, 1, 0, 0);
            }

            using (new ProfilingScope(context.cmd, Profiling.Culling))
            {
                context.cmd.SetComputeVectorArrayParam(_gpuMeshletCullingCS, ShaderID.Culling._CameraFrustumPlanes, data.FrustumPlanes);
                context.cmd.SetComputeVectorParam(_gpuMeshletCullingCS, ShaderID.Culling._CameraPosition, data.CameraPosition);
                context.cmd.SetComputeMatrixParam(_gpuMeshletCullingCS, ShaderID.Culling._CameraViewProjection, data.CameraViewProjectionMatrix);

                context.cmd.SetComputeBufferParam(_gpuMeshletCullingCS, AAAAGPUMeshletCulling.KernelIndex,
                    ShaderID.Culling._DestinationMeshletsCounter, data.DestinationMeshletsCounterBuffer
                );
                context.cmd.SetComputeBufferParam(_gpuMeshletCullingCS, AAAAGPUMeshletCulling.KernelIndex,
                    ShaderID.Culling._DestinationMeshlets, data.DestinationMeshletsBuffer
                );

                context.cmd.DispatchCompute(_gpuMeshletCullingCS, AAAAGPUMeshletCulling.KernelIndex,
                    1, instanceCount, 1
                );
            }

            using (new ProfilingScope(context.cmd, Profiling.FixupIndirectArgs))
            {
                context.cmd.SetComputeBufferParam(_fixupMeshletIndirectDrawArgsCS, AAAAFixupMeshletIndirectDrawArgs.KernelIndex,
                    ShaderID.FixupIndirectArgs._RequestCounter, data.DestinationMeshletsCounterBuffer
                );
                context.cmd.SetComputeBufferParam(_fixupMeshletIndirectDrawArgsCS, AAAAFixupMeshletIndirectDrawArgs.KernelIndex,
                    ShaderID.FixupIndirectArgs._IndirectArgs, data.VisibilityBufferContainer.IndirectArgsBuffer
                );
                context.cmd.DispatchCompute(_fixupMeshletIndirectDrawArgsCS, AAAAFixupMeshletIndirectDrawArgs.KernelIndex, 1, 1, 1);
            }
        }

        private static class Profiling
        {
            public static readonly ProfilingSampler ClearRenderRequestsCount = new(nameof(ClearRenderRequestsCount));
            public static readonly ProfilingSampler Culling = new(nameof(Culling));
            public static readonly ProfilingSampler FixupIndirectArgs = new(nameof(FixupIndirectArgs));
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderID
        {
            public static class Culling
            {
                public static int _CameraFrustumPlanes = Shader.PropertyToID(nameof(_CameraFrustumPlanes));
                public static int _CameraPosition = Shader.PropertyToID(nameof(_CameraPosition));
                public static int _CameraViewProjection = Shader.PropertyToID(nameof(_CameraViewProjection));

                public static int _DestinationMeshletsCounter = Shader.PropertyToID(nameof(_DestinationMeshletsCounter));
                public static int _DestinationMeshlets = Shader.PropertyToID(nameof(_DestinationMeshlets));
            }

            public static class FixupIndirectArgs
            {
                public static int _RequestCounter = Shader.PropertyToID(nameof(_RequestCounter));
                public static int _IndirectArgs = Shader.PropertyToID(nameof(_IndirectArgs));
            }
        }
    }
}