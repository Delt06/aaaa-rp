﻿using System;
using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.Debugging;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Meshlets;
using DELTation.AAAARP.Renderers;
using DELTation.AAAARP.Utils;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes
{
    public sealed class GPUCullingPass : AAAARenderPass<GPUCullingPass.PassData>, IDisposable
    {
        public enum PassType
        {
            Basic,
            Main,
            FalseNegative,
        }

        [CanBeNull]
        private readonly AAAARenderPipelineDebugDisplaySettings _debugDisplaySettings;

        private readonly ComputeShader _fixupGPUMeshletCullingIndirectDispatchArgsCS;
        private readonly ComputeShader _fixupMeshletIndirectDrawArgsCS;
        private readonly ComputeShader _fixupMeshletListBuildIndirectDispatchArgsCS;
        private readonly ComputeShader _gpuInstanceCullingCS;
        private readonly ComputeShader _gpuMeshletCullingCS;
        private readonly ComputeShader _meshletListBuildCS;
        private readonly PassType _passType;
        private readonly ComputeShader _rawBufferClearCS;
        private NativeList<int> _instanceIndices;

        public GPUCullingPass(PassType passType, AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders runtimeShaders,
            [CanBeNull] AAAARenderPipelineDebugDisplaySettings debugDisplaySettings, string nameTag = null) : base(renderPassEvent)
        {
            _passType = passType;
            _rawBufferClearCS = runtimeShaders.RawBufferClearCS;
            _gpuInstanceCullingCS = runtimeShaders.GPUInstanceCullingCS;
            _fixupMeshletListBuildIndirectDispatchArgsCS = runtimeShaders.FixupMeshletListBuildIndirectDispatchArgsCS;
            _meshletListBuildCS = runtimeShaders.MeshletListBuildCS;
            _fixupGPUMeshletCullingIndirectDispatchArgsCS = runtimeShaders.FixupGPUMeshletCullingIndirectDispatchArgsCS;
            _gpuMeshletCullingCS = runtimeShaders.GPUMeshletCullingCS;
            _fixupMeshletIndirectDrawArgsCS = runtimeShaders.FixupMeshletIndirectDrawArgsCS;
            _instanceIndices = new NativeList<int>(Allocator.Persistent);
            _debugDisplaySettings = debugDisplaySettings;

            Name = AutoName;
            if (nameTag != null)
            {
                Name += "." + nameTag;
            }
            if (_passType != PassType.Basic)
            {
                Name += "." + _passType;
            }
        }

        [CanBeNull]
        public Camera CullingCameraOverride { get; set; }

        public override string Name { get; }
        public CullingViewParameters? CullingViewParametersOverride { get; set; }

        public void Dispose()
        {
            if (_instanceIndices.IsCreated)
            {
                _instanceIndices.Dispose();
            }
        }

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAARendererContainer rendererContainer = renderingData.RendererContainer;

            _instanceIndices.Clear();
            rendererContainer.InstanceDataBuffer.GetInstanceIndices(_instanceIndices);
            passData.InstanceCount = _instanceIndices.Length;

            if (passData.InstanceCount == 0)
            {
                return;
            }

            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            Plane[] frustumPlanes = TempCollections.Planes;
            if (CullingViewParametersOverride != null)
            {
                passData.CullingView = CullingViewParametersOverride.Value;
            }
            else
            {
                Camera camera = CullingCameraOverride != null ? CullingCameraOverride : cameraData.Camera;
                Transform cameraTransform = camera.transform;
                Vector3 cameraPosition = cameraTransform.position;

                Matrix4x4 viewProjectionMatrix = camera.projectionMatrix * camera.worldToCameraMatrix;
                passData.CullingView = new CullingViewParameters
                {
                    ViewProjectionMatrix = viewProjectionMatrix,
                    GPUViewProjectionMatrix = GL.GetGPUProjectionMatrix(viewProjectionMatrix, true),
                    CameraPosition = new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 1),
                    CameraRight = cameraTransform.right,
                    CameraUp = cameraTransform.up,
                    PixelSize = new Vector2(cameraData.ScaledWidth, cameraData.ScaledHeight),
                };
            }

            GeometryUtility.CalculateFrustumPlanes(passData.CullingView.ViewProjectionMatrix, frustumPlanes);

            for (int i = 0; i < frustumPlanes.Length; i++)
            {
                Plane frustumPlane = frustumPlanes[i];
                passData.FrustumPlanes[i] = new Vector4(frustumPlane.normal.x, frustumPlane.normal.y, frustumPlane.normal.z, frustumPlane.distance);
            }

            passData.InstanceIndices = builder.CreateTransientBuffer(
                new BufferDesc(_instanceIndices.Length, sizeof(uint), GraphicsBuffer.Target.Raw)
                {
                    name = nameof(PassData.InstanceIndices),
                }
            );

            GraphicsBuffer meshletRenderRequestsBuffer = rendererContainer.MeshletRenderRequestsBuffer;

            passData.InitialMeshletListCounterBuffer = builder.CreateTransientBuffer(CreateCounterBufferDesc("InitialMeshletListCounter"));
            passData.InitialMeshletListBuffer = builder.CreateTransientBuffer(
                new BufferDesc(meshletRenderRequestsBuffer.count, meshletRenderRequestsBuffer.stride, meshletRenderRequestsBuffer.target)
                {
                    name = nameof(PassData.InitialMeshletListBuffer),
                }
            );
            passData.GPUMeshletCullingIndirectDispatchArgsBuffer = builder.CreateTransientBuffer(
                new BufferDesc(UnsafeUtility.SizeOf<IndirectDispatchArgs>() / sizeof(uint), sizeof(uint),
                    GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw
                )
                {
                    name = nameof(PassData.GPUMeshletCullingIndirectDispatchArgsBuffer),
                }
            );

            passData.MeshletListBuildIndirectDispatchArgsBuffer = builder.CreateTransientBuffer(
                new BufferDesc(UnsafeUtility.SizeOf<IndirectDispatchArgs>() / sizeof(uint), sizeof(uint),
                    GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw
                )
                {
                    name = nameof(PassData.MeshletListBuildIndirectDispatchArgsBuffer),
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

            if (_passType != PassType.Basic)
            {
                OcclusionCullingResources.FrameResources currentFrameResources = rendererContainer.OcclusionCullingResources.GetCurrentFrameResources();
                passData.OcclusionCullingInstanceVisibilityMask = renderingData.RenderGraph.ImportBuffer(currentFrameResources.InstanceVisibilityMask);
                passData.OcclusionCullingInstanceVisibilityMaskCount = rendererContainer.OcclusionCullingResources.InstanceVisibilityMaskItemCount;
            }
            else
            {
                passData.OcclusionCullingInstanceVisibilityMask = default;
                passData.OcclusionCullingInstanceVisibilityMaskCount = default;
            }

            passData.DestinationMeshletsCounterBuffer = builder.CreateTransientBuffer(CreateCounterBufferDesc("MeshletRenderRequestCounter"));
            passData.DestinationMeshletsBuffer = builder.WriteBuffer(renderingData.RenderGraph.ImportBuffer(meshletRenderRequestsBuffer));

            passData.IndirectDrawArgsBuffer = builder.WriteBuffer(renderingData.RenderGraph.ImportBuffer(rendererContainer.IndirectDrawArgsBuffer));

            if (_passType == PassType.FalseNegative)
            {
                AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
                builder.ReadTexture(resourceData.CameraScaledDepthBuffer);
                builder.ReadTexture(resourceData.CameraHZBScaled);
            }

            passData.DebugDataBuffer = _debugDisplaySettings is { RenderingSettings: { DebugGPUCulling: true } }
                ? builder.ReadBuffer(frameData.Get<AAAADebugData>().GPUCullingDebugBuffer)
                : default;
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

                if (data.OcclusionCullingInstanceVisibilityMask.IsValid())
                {
                    AAAARawBufferClear.DispatchClear(context.cmd, _rawBufferClearCS, data.OcclusionCullingInstanceVisibilityMask,
                        data.OcclusionCullingInstanceVisibilityMaskCount, 0, 0
                    );
                }
            }

            using (new ProfilingScope(context.cmd, Profiling.InstanceCulling))
            {
                const int kernelIndex = 0;

                context.cmd.SetKeyword(_gpuInstanceCullingCS,
                    new LocalKeyword(_gpuInstanceCullingCS, Keywords.MAIN_PASS), _passType == PassType.Main
                );
                context.cmd.SetKeyword(_gpuInstanceCullingCS,
                    new LocalKeyword(_gpuInstanceCullingCS, Keywords.FALSE_NEGATIVE_PASS), _passType == PassType.FalseNegative
                );
                context.cmd.SetKeyword(_gpuInstanceCullingCS,
                    new LocalKeyword(_gpuInstanceCullingCS, Keywords.Debug.DEBUG_GPU_CULLING), data.DebugDataBuffer.IsValid()
                );

                context.cmd.SetComputeVectorArrayParam(_gpuInstanceCullingCS, ShaderID.GPUInstanceCulling._CameraFrustumPlanes, data.FrustumPlanes);
                context.cmd.SetComputeMatrixParam(_gpuInstanceCullingCS, ShaderID.GPUInstanceCulling._CameraViewProjection,
                    data.CullingView.GPUViewProjectionMatrix
                );

                context.cmd.SetBufferData(data.InstanceIndices, _instanceIndices.AsArray());
                context.cmd.SetComputeBufferParam(_gpuInstanceCullingCS, kernelIndex,
                    ShaderID.GPUInstanceCulling._InstanceIndices, data.InstanceIndices
                );
                context.cmd.SetComputeIntParam(_gpuInstanceCullingCS,
                    ShaderID.GPUInstanceCulling._InstanceIndicesCount, data.InstanceCount
                );

                context.cmd.SetComputeBufferParam(_gpuInstanceCullingCS, kernelIndex,
                    ShaderID.GPUInstanceCulling._Jobs, data.MeshletListBuildJobsBuffer
                );
                context.cmd.SetComputeBufferParam(_gpuInstanceCullingCS, kernelIndex,
                    ShaderID.GPUInstanceCulling._JobCounter, data.MeshletListBuildJobCounterBuffer
                );

                if (data.DebugDataBuffer.IsValid())
                {
                    context.cmd.SetComputeBufferParam(_gpuInstanceCullingCS, kernelIndex,
                        ShaderID.Debug._GPUCullingDebugDataBuffer, data.DebugDataBuffer
                    );
                }

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

                context.cmd.SetComputeMatrixParam(_meshletListBuildCS, ShaderID.MeshletListBuild._CameraViewProjection, data.CullingView.GPUViewProjectionMatrix
                );
                context.cmd.SetComputeVectorParam(_meshletListBuildCS, ShaderID.MeshletListBuild._CameraPosition, data.CullingView.CameraPosition);
                context.cmd.SetComputeVectorParam(_meshletListBuildCS, ShaderID.MeshletListBuild._CameraUp, data.CullingView.CameraUp);
                context.cmd.SetComputeVectorParam(_meshletListBuildCS, ShaderID.MeshletListBuild._CameraRight, data.CullingView.CameraRight);
                context.cmd.SetComputeVectorParam(_meshletListBuildCS, ShaderID.MeshletListBuild._ScreenSizePixels, data.CullingView.PixelSize);

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

                context.cmd.SetKeyword(_gpuMeshletCullingCS,
                    new LocalKeyword(_gpuMeshletCullingCS, Keywords.MAIN_PASS), _passType == PassType.Main
                );
                context.cmd.SetKeyword(_gpuMeshletCullingCS,
                    new LocalKeyword(_gpuMeshletCullingCS, Keywords.FALSE_NEGATIVE_PASS), _passType == PassType.FalseNegative
                );
                context.cmd.SetKeyword(_gpuMeshletCullingCS,
                    new LocalKeyword(_gpuMeshletCullingCS, Keywords.Debug.DEBUG_GPU_CULLING), data.DebugDataBuffer.IsValid()
                );

                context.cmd.SetComputeVectorArrayParam(_gpuMeshletCullingCS, ShaderID.MeshletCulling._CameraFrustumPlanes, data.FrustumPlanes);
                context.cmd.SetComputeVectorParam(_gpuMeshletCullingCS, ShaderID.MeshletCulling._CameraPosition, data.CullingView.CameraPosition);
                context.cmd.SetComputeMatrixParam(_gpuMeshletCullingCS, ShaderID.MeshletCulling._CameraViewProjection, data.CullingView.GPUViewProjectionMatrix
                );

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

                if (data.DebugDataBuffer.IsValid())
                {
                    context.cmd.SetComputeBufferParam(_gpuMeshletCullingCS, kernelIndex,
                        ShaderID.Debug._GPUCullingDebugDataBuffer, data.DebugDataBuffer
                    );
                }

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

        public struct CullingViewParameters
        {
            public Vector3 CameraPosition;
            public Vector3 CameraUp;
            public Vector3 CameraRight;
            public Vector2 PixelSize;
            public Matrix4x4 ViewProjectionMatrix;
            public Matrix4x4 GPUViewProjectionMatrix;
        }

        public class PassData : PassDataBase
        {
            public readonly Vector4[] FrustumPlanes = new Vector4[6];

            public CullingViewParameters CullingView;

            public BufferHandle DebugDataBuffer;

            public BufferHandle DestinationMeshletsBuffer;
            public BufferHandle DestinationMeshletsCounterBuffer;
            public BufferHandle GPUMeshletCullingIndirectDispatchArgsBuffer;
            public BufferHandle IndirectDrawArgsBuffer;

            public BufferHandle InitialMeshletListBuffer;
            public BufferHandle InitialMeshletListCounterBuffer;

            public int InstanceCount;

            public BufferHandle InstanceIndices;

            public BufferHandle MeshletListBuildIndirectDispatchArgsBuffer;
            public BufferHandle MeshletListBuildJobCounterBuffer;
            public BufferHandle MeshletListBuildJobsBuffer;

            public BufferHandle OcclusionCullingInstanceVisibilityMask;
            public int OcclusionCullingInstanceVisibilityMaskCount;
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
        private static class Keywords
        {
            public static string MAIN_PASS = nameof(MAIN_PASS);
            public static string FALSE_NEGATIVE_PASS = nameof(FALSE_NEGATIVE_PASS);

            public static class Debug
            {
                public static string DEBUG_GPU_CULLING = nameof(DEBUG_GPU_CULLING);
            }
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderID
        {
            public static class Debug
            {
                public static int _GPUCullingDebugDataBuffer = Shader.PropertyToID(nameof(_GPUCullingDebugDataBuffer));
            }

            public static class GPUInstanceCulling
            {
                public static int _CameraFrustumPlanes = Shader.PropertyToID(nameof(_CameraFrustumPlanes));
                public static int _CameraViewProjection = Shader.PropertyToID(nameof(_CameraViewProjection));

                public static int _InstanceIndices = Shader.PropertyToID(nameof(_InstanceIndices));
                public static int _InstanceIndicesCount = Shader.PropertyToID(nameof(_InstanceIndicesCount));

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
                public static int _CameraViewProjection = Shader.PropertyToID(nameof(_CameraViewProjection));

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