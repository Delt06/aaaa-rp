﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.Debugging;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Meshlets;
using DELTation.AAAARP.Renderers;
using DELTation.AAAARP.RenderPipelineResources;
using DELTation.AAAARP.Utils;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
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
            Voxelization,
        }

        [CanBeNull]
        private readonly AAAARenderPipelineDebugDisplaySettings _debugDisplaySettings;

        private readonly ComputeShader _fixupGPUMeshletCullingIndirectDispatchArgsCS;
        private readonly ComputeShader _gpuInstanceCullingCS;
        private readonly ComputeShader _gpuMeshletCullingCS;
        private readonly ComputeShader _meshletListBuildCS;
        private readonly PassType _passType;
        private readonly AAAARawBufferClear _rawBufferClear;
        private NativeList<int> _instanceIndices;

        public GPUCullingPass(PassType passType, AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders runtimeShaders,
            AAAARawBufferClear rawBufferClear,
            [CanBeNull] AAAARenderPipelineDebugDisplaySettings debugDisplaySettings, string nameTag = null, string namePrefix = null) : base(renderPassEvent)
        {
            _passType = passType;
            _gpuInstanceCullingCS = runtimeShaders.GPUInstanceCullingCS;
            _meshletListBuildCS = runtimeShaders.MeshletListBuildCS;
            _fixupGPUMeshletCullingIndirectDispatchArgsCS = runtimeShaders.FixupGPUMeshletCullingIndirectDispatchArgsCS;
            _gpuMeshletCullingCS = runtimeShaders.GPUMeshletCullingCS;
            _rawBufferClear = rawBufferClear;
            _instanceIndices = new NativeList<int>(Allocator.Persistent);
            _debugDisplaySettings = debugDisplaySettings;

            Name = AutoName;
            if (namePrefix != null)
            {
                Name = namePrefix + Name;
            }
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
        public List<CullingViewParameters> CullingContextParameterList { get; } = new();

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
            Camera camera = CullingCameraOverride != null ? CullingCameraOverride : cameraData.Camera;
            Transform cameraTransform = camera.transform;
            Vector3 cameraPosition = cameraTransform.position;

            Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
            Matrix4x4 viewProjectionMatrix = camera.projectionMatrix * viewMatrix;
            Matrix4x4 gpuViewProjectionMatrix = GL.GetGPUProjectionMatrix(viewProjectionMatrix, true);

            var pixelSize = new Vector2(cameraData.ScaledWidth, cameraData.ScaledHeight);
            Vector3 cameraForward = cameraTransform.forward;
            Vector3 cameraRight = cameraTransform.right;
            Vector3 cameraUp = cameraTransform.up;

            passData.CullingContextCount = 0;
            passData.DisableOcclusionCulling = CullingCameraOverride != null;

            bool noOverrides = CullingContextParameterList.Count == 0;

            if (noOverrides)
            {
                CullingContextParameterList.Add(new CullingViewParameters
                    {
                        ViewMatrix = viewMatrix,
                        ViewProjectionMatrix = viewProjectionMatrix,
                        GPUViewProjectionMatrix = gpuViewProjectionMatrix,
                        CameraPosition = cameraPosition,
                        CameraForward = cameraForward,
                        CameraRight = cameraRight,
                        CameraUp = cameraUp,
                        PixelSize = pixelSize,
                        IsPerspective = !camera.orthographic,
                        BoundingSphereWS = math.float4(0, 0, 0, 0),
                        PassMask = AAAAInstancePassMask.Main,
                    }
                );
            }

            passData.IndirectDrawArgsOffset = 0;

            for (int contextIndex = 0; contextIndex < CullingContextParameterList.Count; contextIndex++)
            {
                CullingViewParameters cullingViewParameters = CullingContextParameterList[contextIndex];
                PassData.CullingContext cullingContext = passData.CullingContexts[passData.CullingContextCount++];

                cullingContext.MeshletRenderRequestsOffset = contextIndex * rendererContainer.MeshletRenderRequestByteStridePerContext;
                cullingContext.ViewParameters = cullingViewParameters;

                // LOD for shadows should be the same as for main view.
                // We do not explicitly synchronize LOD selection, just the input parameters.
                cullingContext.LODSelectionContext = new LODSelectionContext
                {
                    CameraPosition = cameraPosition,
                    CameraRight = cameraRight,
                    CameraUp = cameraUp,
                    PixelSize = pixelSize,
                    GPUViewProjectionMatrix = gpuViewProjectionMatrix,
                };

                Plane[] frustumPlanes = TempCollections.Planes;
                GeometryUtility.CalculateFrustumPlanes(cullingContext.ViewParameters.ViewProjectionMatrix, frustumPlanes);

                for (int i = 0; i < frustumPlanes.Length; i++)
                {
                    Plane frustumPlane = frustumPlanes[i];
                    cullingContext.FrustumPlanes[i] = new Vector4(frustumPlane.normal.x, frustumPlane.normal.y, frustumPlane.normal.z, frustumPlane.distance);
                }

                cullingContext.CullingSphereLS = float4.zero;
                if (cullingContext.ViewParameters.BoundingSphereWS.w > 0.0f)
                {
                    cullingContext.CullingSphereLS.xyz = math.transform(cullingContext.ViewParameters.ViewMatrix,
                        cullingContext.ViewParameters.BoundingSphereWS.xyz
                    );
                    cullingContext.CullingSphereLS.w = cullingContext.ViewParameters.BoundingSphereWS.w;
                }

                cullingContext.MeshletListBuildJobsOffset = contextIndex * rendererContainer.MaxMeshletListBuildJobCount;
            }

            if (noOverrides)
            {
                CullingContextParameterList.Clear();
            }

            passData.InstanceIndices = builder.CreateTransientBuffer(
                new BufferDesc(_instanceIndices.Length, sizeof(uint), GraphicsBuffer.Target.Raw)
                {
                    name = nameof(PassData.InstanceIndices),
                }
            );

            GraphicsBuffer meshletRenderRequestsBuffer = rendererContainer.MeshletRenderRequestsBuffer;

            passData.InitialMeshletListCountersBuffer =
                builder.CreateTransientBuffer(
                    new BufferDesc(GPUCullingContext.MaxCullingContextsPerBatch, sizeof(uint),
                        GraphicsBuffer.Target.Raw | GraphicsBuffer.Target.CopyDestination
                    )
                    {
                        name = nameof(PassData.InitialMeshletListCountersBuffer),
                    }
                );
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

            passData.RendererListMeshletCountsBuffer = builder.CreateTransientBuffer(
                new BufferDesc(GPUCullingContext.MaxCullingContextsPerBatch * (int) AAAARendererListID.Count, sizeof(uint),
                    GraphicsBuffer.Target.Raw | GraphicsBuffer.Target.CopyDestination
                )
                {
                    name = nameof(PassData.RendererListMeshletCountsBuffer),
                }
            );
            passData.MeshletListBuildIndirectDispatchArgsBuffer = builder.CreateTransientBuffer(
                new BufferDesc(
                    UnsafeUtility.SizeOf<IndirectDispatchArgs>() / sizeof(uint), sizeof(uint),
                    GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw
                )
                {
                    name = nameof(PassData.MeshletListBuildIndirectDispatchArgsBuffer),
                }
            );
            passData.MeshletListBuildJobsBuffer = builder.CreateTransientBuffer(
                new BufferDesc(GPUCullingContext.MaxCullingContextsPerBatch * rendererContainer.MaxMeshletListBuildJobCount,
                    UnsafeUtility.SizeOf<AAAAMeshletListBuildJob>(),
                    GraphicsBuffer.Target.Structured
                )
                {
                    name = nameof(PassData.MeshletListBuildJobsBuffer),
                }
            );
            passData.MeshletListBuildJobCountersBuffer = builder.CreateTransientBuffer(
                new BufferDesc(GPUCullingContext.MaxCullingContextsPerBatch,
                    sizeof(uint),
                    GraphicsBuffer.Target.Raw | GraphicsBuffer.Target.CopyDestination
                )
                {
                    name = nameof(PassData.MeshletListBuildJobCountersBuffer),
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

            passData.DestinationMeshletsBuffer = builder.WriteBuffer(renderingData.RenderGraph.ImportBuffer(meshletRenderRequestsBuffer));

            passData.IndirectDrawArgsBuffer = builder.WriteBuffer(renderingData.RenderGraph.ImportBuffer(rendererContainer.IndirectDrawArgsBuffer));

            passData.GPUCullingContextBuffer = builder.CreateTransientBuffer(builder.CreateTransientBuffer(
                    new BufferDesc(GPUCullingContext.MaxCullingContextsPerBatch, UnsafeUtility.SizeOf<GPUCullingContext>(), GraphicsBuffer.Target.Constant)
                    {
                        name = nameof(PassData.GPUCullingContextBuffer),
                    }
                )
            );
            passData.GPULODSelectionContextBuffer = builder.CreateTransientBuffer(builder.CreateTransientBuffer(
                    new BufferDesc(GPUCullingContext.MaxCullingContextsPerBatch, UnsafeUtility.SizeOf<GPULODSelectionContext>(), GraphicsBuffer.Target.Constant)
                    {
                        name = nameof(PassData.GPULODSelectionContextBuffer),
                    }
                )
            );

            if (_passType == PassType.FalseNegative)
            {
                AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
                builder.ReadTexture(resourceData.CameraScaledDepthBuffer);
                builder.ReadTexture(resourceData.CameraHZBScaled);
            }

            passData.DebugDataBuffer = _debugDisplaySettings is { RenderingSettings: { DebugGPUCulling: true } } && CullingContextParameterList == null
                ? builder.ReadBuffer(frameData.Get<AAAADebugData>().GPUCullingDebugBuffer)
                : default;
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            if (data.InstanceCount == 0)
            {
                return;
            }

            using (new ProfilingScope(context.cmd, Profiling.InitBuffers))
            {
                {
                    const Allocator allocator = Allocator.Temp;
                    const NativeArrayOptions arrayOptions = NativeArrayOptions.UninitializedMemory;
                    var cullingContextsData = new NativeArray<GPUCullingContext>(data.CullingContextCount, allocator, arrayOptions);
                    var lodSelectionContextsData = new NativeArray<GPULODSelectionContext>(data.CullingContextCount, allocator, arrayOptions);

                    for (int contextIndex = 0; contextIndex < data.CullingContextCount; contextIndex++)
                    {
                        PassData.CullingContext cullingContext = data.CullingContexts[contextIndex];
                        ConstantBufferUtils.FillGPUCullingContext(ref cullingContextsData.ElementAtRef(contextIndex), cullingContext);
                        ConstantBufferUtils.FillGPULODSelectionContext(ref lodSelectionContextsData.ElementAtRef(contextIndex), cullingContext);
                    }

                    context.cmd.SetBufferData(data.GPUCullingContextBuffer, cullingContextsData);
                    context.cmd.SetBufferData(data.GPULODSelectionContextBuffer, lodSelectionContextsData);
                }

                _rawBufferClear.FastZeroClear(context.cmd, data.InitialMeshletListCountersBuffer,
                    GPUCullingContext.MaxCullingContextsPerBatch
                );
                const int rendererListCount = (int) AAAARendererListID.Count;
                _rawBufferClear.FastZeroClear(context.cmd, data.RendererListMeshletCountsBuffer,
                    rendererListCount * GPUCullingContext.MaxCullingContextsPerBatch
                );

                {
                    var initialIndirectDispatchArgs =
                        new NativeArray<IndirectDispatchArgs>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                    for (int i = 0; i < initialIndirectDispatchArgs.Length; i++)
                    {
                        initialIndirectDispatchArgs[i] = new IndirectDispatchArgs
                        {
                            ThreadGroupsX = 0,
                            ThreadGroupsY = 1,
                            ThreadGroupsZ = 1,
                        };
                    }
                    context.cmd.SetBufferData(data.MeshletListBuildIndirectDispatchArgsBuffer, initialIndirectDispatchArgs);

                    _rawBufferClear.FastZeroClear(context.cmd, data.MeshletListBuildJobCountersBuffer, GPUCullingContext.MaxCullingContextsPerBatch);
                }

                {
                    var initialIndirectDrawArgs =
                        new NativeArray<GraphicsBuffer.IndirectDrawArgs>(data.CullingContextCount * rendererListCount, Allocator.Temp,
                            NativeArrayOptions.UninitializedMemory
                        );
                    for (int argsIndex = 0; argsIndex < initialIndirectDrawArgs.Length; argsIndex++)
                    {
                        initialIndirectDrawArgs[argsIndex] = new GraphicsBuffer.IndirectDrawArgs
                        {
                            startInstance = 0,
                            instanceCount = 0,
                            startVertex = 0,
                            vertexCountPerInstance = AAAAMeshletConfiguration.MaxMeshletIndices,
                        };
                    }
                    const int nativeBufferStartIndex = 0;
                    int graphicsBufferStartIndex = data.IndirectDrawArgsOffset / UnsafeUtility.SizeOf<GraphicsBuffer.IndirectDrawArgs>();
                    context.cmd.SetBufferData(data.IndirectDrawArgsBuffer, initialIndirectDrawArgs,
                        nativeBufferStartIndex, graphicsBufferStartIndex, initialIndirectDrawArgs.Length
                    );
                }

                if (data.OcclusionCullingInstanceVisibilityMask.IsValid())
                {
                    _rawBufferClear.DispatchClear(context.cmd, data.OcclusionCullingInstanceVisibilityMask,
                        data.OcclusionCullingInstanceVisibilityMaskCount, 0, 0
                    );
                }
            }

            using (new ProfilingScope(context.cmd, Profiling.InstanceCulling))
            {
                const int kernelIndex = 0;

                CoreUtils.SetKeyword(context.cmd, _gpuInstanceCullingCS, Keywords.MAIN_PASS, _passType == PassType.Main);
                CoreUtils.SetKeyword(context.cmd, _gpuInstanceCullingCS, Keywords.FALSE_NEGATIVE_PASS, _passType == PassType.FalseNegative);
                CoreUtils.SetKeyword(context.cmd, _gpuInstanceCullingCS, Keywords.VOXELIZATION_PASS, _passType == PassType.Voxelization);
                CoreUtils.SetKeyword(context.cmd, _gpuInstanceCullingCS, Keywords.DISABLE_OCCLUSION_CULLING, data.DisableOcclusionCulling);
                CoreUtils.SetKeyword(context.cmd, _gpuInstanceCullingCS, Keywords.Debug.DEBUG_GPU_CULLING, data.DebugDataBuffer.IsValid());

                context.cmd.SetComputeConstantBufferParam(_gpuInstanceCullingCS,
                    ShaderID.GPUInstanceCulling._CullingContexts, data.GPUCullingContextBuffer,
                    0, UnsafeUtility.SizeOf<GPUCullingContext>() * data.CullingContextCount
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
                    ShaderID.GPUInstanceCulling._JobCounters, data.MeshletListBuildJobCountersBuffer
                );
                context.cmd.SetComputeBufferParam(_gpuInstanceCullingCS, kernelIndex,
                    ShaderID.GPUInstanceCulling._MeshletListBuildIndirectArgs, data.MeshletListBuildIndirectDispatchArgsBuffer
                );

                if (data.DebugDataBuffer.IsValid())
                {
                    context.cmd.SetComputeBufferParam(_gpuInstanceCullingCS, kernelIndex,
                        ShaderID.Debug._GPUCullingDebugDataBuffer, data.DebugDataBuffer
                    );
                }

                const int groupSize = (int) AAAAMeshletComputeShaders.GPUInstanceCullingThreadGroupSize;
                context.cmd.DispatchCompute(_gpuInstanceCullingCS, kernelIndex,
                    AAAAMathUtils.AlignUp(data.InstanceCount, groupSize) / groupSize, data.CullingContextCount, 1
                );
            }

            using (new ProfilingScope(context.cmd, Profiling.MeshletListBuild))
            {
                const int kernelIndex = 0;

                CoreUtils.SetKeyword(context.cmd, _meshletListBuildCS, Keywords.VOXELIZATION_PASS, _passType == PassType.Voxelization);

                context.cmd.SetComputeConstantBufferParam(_meshletListBuildCS,
                    ShaderID.MeshletListBuild._CullingContexts, data.GPUCullingContextBuffer,
                    0, UnsafeUtility.SizeOf<GPUCullingContext>() * data.CullingContextCount
                );
                context.cmd.SetComputeConstantBufferParam(_meshletListBuildCS,
                    ShaderID.MeshletListBuild._LODSelectionContexts, data.GPULODSelectionContextBuffer,
                    0, UnsafeUtility.SizeOf<GPULODSelectionContext>() * data.CullingContextCount
                );

                context.cmd.SetComputeBufferParam(_meshletListBuildCS, kernelIndex,
                    ShaderID.MeshletListBuild._Jobs, data.MeshletListBuildJobsBuffer
                );
                context.cmd.SetComputeBufferParam(_meshletListBuildCS, kernelIndex,
                    ShaderID.MeshletListBuild._JobCounters, data.MeshletListBuildJobCountersBuffer
                );
                context.cmd.SetComputeBufferParam(_meshletListBuildCS, kernelIndex,
                    ShaderID.MeshletListBuild._DestinationMeshletsCounter, data.InitialMeshletListCountersBuffer
                );
                context.cmd.SetComputeBufferParam(_meshletListBuildCS, kernelIndex,
                    ShaderID.MeshletListBuild._DestinationMeshlets, data.InitialMeshletListBuffer
                );
                context.cmd.SetComputeBufferParam(_meshletListBuildCS, kernelIndex,
                    ShaderID.MeshletListBuild._RendererListMeshletCounts, data.RendererListMeshletCountsBuffer
                );

                context.cmd.DispatchCompute(_meshletListBuildCS, kernelIndex, data.MeshletListBuildIndirectDispatchArgsBuffer, 0);
            }

            using (new ProfilingScope(context.cmd, Profiling.FixupMeshletCullingIndirectDispatchArgs))
            {
                const int kernelIndex = 0;

                context.cmd.SetComputeConstantBufferParam(_fixupGPUMeshletCullingIndirectDispatchArgsCS,
                    ShaderID.FixupMeshletCullingIndirectDispatchArgs._CullingContexts, data.GPUCullingContextBuffer,
                    0, UnsafeUtility.SizeOf<GPUCullingContext>() * data.CullingContextCount
                );
                context.cmd.SetComputeIntParam(_fixupGPUMeshletCullingIndirectDispatchArgsCS,
                    ShaderID.FixupMeshletCullingIndirectDispatchArgs._CullingContextCount, data.CullingContextCount
                );

                context.cmd.SetComputeBufferParam(_fixupGPUMeshletCullingIndirectDispatchArgsCS, kernelIndex,
                    ShaderID.FixupMeshletCullingIndirectDispatchArgs._RequestCounters, data.InitialMeshletListCountersBuffer
                );
                context.cmd.SetComputeBufferParam(_fixupGPUMeshletCullingIndirectDispatchArgsCS, kernelIndex,
                    ShaderID.FixupMeshletCullingIndirectDispatchArgs._IndirectArgs, data.GPUMeshletCullingIndirectDispatchArgsBuffer
                );

                context.cmd.SetComputeIntParam(_fixupGPUMeshletCullingIndirectDispatchArgsCS,
                    ShaderID.FixupMeshletCullingIndirectDispatchArgs._IndirectDrawArgsOffset, data.IndirectDrawArgsOffset
                );
                context.cmd.SetComputeBufferParam(_fixupGPUMeshletCullingIndirectDispatchArgsCS, kernelIndex,
                    ShaderID.FixupMeshletCullingIndirectDispatchArgs._RendererListMeshletCounts, data.RendererListMeshletCountsBuffer
                );
                context.cmd.SetComputeBufferParam(_fixupGPUMeshletCullingIndirectDispatchArgsCS, kernelIndex,
                    ShaderID.FixupMeshletCullingIndirectDispatchArgs._IndirectDrawArgs, data.IndirectDrawArgsBuffer
                );

                context.cmd.DispatchCompute(_fixupGPUMeshletCullingIndirectDispatchArgsCS, kernelIndex, 1, 1, 1);
            }

            using (new ProfilingScope(context.cmd, Profiling.MeshletCulling))
            {
                const int kernelIndex = 0;

                CoreUtils.SetKeyword(context.cmd, _gpuMeshletCullingCS, Keywords.MAIN_PASS, _passType == PassType.Main);
                CoreUtils.SetKeyword(context.cmd, _gpuMeshletCullingCS, Keywords.FALSE_NEGATIVE_PASS, _passType == PassType.FalseNegative);
                CoreUtils.SetKeyword(context.cmd, _gpuMeshletCullingCS, Keywords.VOXELIZATION_PASS, _passType == PassType.Voxelization);
                CoreUtils.SetKeyword(context.cmd, _gpuMeshletCullingCS, Keywords.DISABLE_OCCLUSION_CULLING, data.DisableOcclusionCulling);
                CoreUtils.SetKeyword(context.cmd, _gpuMeshletCullingCS, Keywords.Debug.DEBUG_GPU_CULLING, data.DebugDataBuffer.IsValid());

                context.cmd.SetComputeConstantBufferParam(_gpuMeshletCullingCS,
                    ShaderID.MeshletCulling._CullingContexts, data.GPUCullingContextBuffer,
                    0, UnsafeUtility.SizeOf<GPUCullingContext>() * data.CullingContextCount
                );

                context.cmd.SetComputeBufferParam(_gpuMeshletCullingCS, kernelIndex,
                    ShaderID.MeshletCulling._SourceMeshletsCounters, data.InitialMeshletListCountersBuffer
                );
                context.cmd.SetComputeBufferParam(_gpuMeshletCullingCS, kernelIndex,
                    ShaderID.MeshletCulling._SourceMeshlets, data.InitialMeshletListBuffer
                );

                context.cmd.SetComputeBufferParam(_gpuMeshletCullingCS, kernelIndex,
                    ShaderID.MeshletCulling._IndirectDrawArgs, data.IndirectDrawArgsBuffer
                );
                context.cmd.SetComputeIntParam(_gpuMeshletCullingCS,
                    ShaderID.MeshletCulling._IndirectDrawArgsOffset, data.IndirectDrawArgsOffset
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
        }

        private static class ConstantBufferUtils
        {
            public static unsafe void FillGPUCullingContext(ref GPUCullingContext gpuCullingContext, in PassData.CullingContext cullingContext)
            {
                CullingViewParameters viewParameters = cullingContext.ViewParameters;

                gpuCullingContext = new GPUCullingContext
                {
                    ViewMatrix = viewParameters.ViewMatrix,
                    ViewProjectionMatrix = viewParameters.GPUViewProjectionMatrix,
                    CameraPosition = math.float4(viewParameters.CameraPosition, 1),
                    CullingSphereLS = cullingContext.CullingSphereLS,
                    PassMask = (int) viewParameters.PassMask,
                    CameraIsPerspective = viewParameters.IsPerspective ? 1 : 0,
                    BaseStartInstance = (uint) (cullingContext.MeshletRenderRequestsOffset / UnsafeUtility.SizeOf<AAAAMeshletRenderRequestPacked>()),
                    MeshletListBuildJobsOffset = (uint) cullingContext.MeshletListBuildJobsOffset,
                    MeshletRenderRequestsOffset = (uint) cullingContext.MeshletRenderRequestsOffset,
                };

                fixed (float* pFrustumPlanesDestination = gpuCullingContext.FrustumPlanes)
                {
                    Vector4[] frustumPlanes = cullingContext.FrustumPlanes;
                    fixed (Vector4* pFrustumPlanesSource = frustumPlanes)
                    {
                        UnsafeUtility.MemCpy(pFrustumPlanesDestination, pFrustumPlanesSource, UnsafeUtility.SizeOf<Vector4>() * frustumPlanes.Length);
                    }
                }
            }

            public static void FillGPULODSelectionContext(ref GPULODSelectionContext gpuLodSelectionContext, in PassData.CullingContext cullingContext)
            {
                LODSelectionContext lodSelectionContext = cullingContext.LODSelectionContext;

                gpuLodSelectionContext = new GPULODSelectionContext
                {
                    CameraPosition = math.float4(lodSelectionContext.CameraPosition, 1),
                    CameraRight = math.float4(lodSelectionContext.CameraRight, 0),
                    CameraUp = math.float4(lodSelectionContext.CameraUp, 0),
                    ViewProjectionMatrix = lodSelectionContext.GPUViewProjectionMatrix,
                    ScreenSizePixels = lodSelectionContext.PixelSize,
                };
            }
        }

        public struct CullingViewParameters
        {
            public Vector3 CameraPosition;
            public Vector3 CameraForward;
            public Vector3 CameraUp;
            public Vector3 CameraRight;
            public Vector2 PixelSize;
            public Matrix4x4 ViewProjectionMatrix;
            public Matrix4x4 ViewMatrix;
            public Matrix4x4 GPUViewProjectionMatrix;
            public float4 BoundingSphereWS;
            public bool IsPerspective;
            public AAAAInstancePassMask PassMask;
        }

        public class PassData : PassDataBase
        {
            public readonly CullingContext[] CullingContexts = new CullingContext[GPUCullingContext.MaxCullingContextsPerBatch];
            public int CullingContextCount;

            public BufferHandle DebugDataBuffer;

            public BufferHandle DestinationMeshletsBuffer;
            public bool DisableOcclusionCulling;
            public BufferHandle GPUCullingContextBuffer;
            public BufferHandle GPULODSelectionContextBuffer;
            public BufferHandle GPUMeshletCullingIndirectDispatchArgsBuffer;
            public BufferHandle IndirectDrawArgsBuffer;
            public int IndirectDrawArgsOffset;

            public BufferHandle InitialMeshletListBuffer;
            public BufferHandle InitialMeshletListCountersBuffer;

            public int InstanceCount;

            public BufferHandle InstanceIndices;

            public BufferHandle MeshletListBuildIndirectDispatchArgsBuffer;
            public BufferHandle MeshletListBuildJobCountersBuffer;
            public BufferHandle MeshletListBuildJobsBuffer;

            public BufferHandle OcclusionCullingInstanceVisibilityMask;
            public int OcclusionCullingInstanceVisibilityMaskCount;

            public BufferHandle RendererListMeshletCountsBuffer;

            public PassData()
            {
                for (int i = 0; i < CullingContexts.Length; i++)
                {
                    CullingContexts[i] = new CullingContext();
                }
            }

            public class CullingContext
            {
                public readonly Vector4[] FrustumPlanes = new Vector4[6];
                public float4 CullingSphereLS;
                public LODSelectionContext LODSelectionContext;
                public int MeshletListBuildJobsOffset;
                public int MeshletRenderRequestsOffset;
                public CullingViewParameters ViewParameters;
            }
        }

        public struct LODSelectionContext
        {
            public Matrix4x4 GPUViewProjectionMatrix;
            public Vector3 CameraPosition;
            public Vector3 CameraUp;
            public Vector3 CameraRight;
            public Vector2 PixelSize;
        }

        private static class Profiling
        {
            public static readonly ProfilingSampler InitBuffers = new(nameof(InitBuffers));
            public static readonly ProfilingSampler InstanceCulling = new(nameof(InstanceCulling));
            public static readonly ProfilingSampler MeshletListBuild = new(nameof(MeshletListBuild));
            public static readonly ProfilingSampler FixupMeshletCullingIndirectDispatchArgs = new(nameof(FixupMeshletCullingIndirectDispatchArgs));
            public static readonly ProfilingSampler MeshletCulling = new(nameof(MeshletCulling));
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class Keywords
        {
            public static string MAIN_PASS = nameof(MAIN_PASS);
            public static string FALSE_NEGATIVE_PASS = nameof(FALSE_NEGATIVE_PASS);
            public static string VOXELIZATION_PASS = nameof(VOXELIZATION_PASS);
            public static string DISABLE_OCCLUSION_CULLING = nameof(DISABLE_OCCLUSION_CULLING);

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
                public static int _CullingContexts = Shader.PropertyToID(nameof(_CullingContexts));

                public static int _InstanceIndices = Shader.PropertyToID(nameof(_InstanceIndices));
                public static int _InstanceIndicesCount = Shader.PropertyToID(nameof(_InstanceIndicesCount));

                public static int _Jobs = Shader.PropertyToID(nameof(_Jobs));
                public static int _JobCounters = Shader.PropertyToID(nameof(_JobCounters));
                public static int _MeshletListBuildIndirectArgs = Shader.PropertyToID(nameof(_MeshletListBuildIndirectArgs));
            }

            public static class MeshletListBuild
            {
                public static int _CullingContexts = Shader.PropertyToID(nameof(_CullingContexts));
                public static int _LODSelectionContexts = Shader.PropertyToID(nameof(_LODSelectionContexts));

                public static int _Jobs = Shader.PropertyToID(nameof(_Jobs));
                public static int _JobCounters = Shader.PropertyToID(nameof(_JobCounters));

                public static int _DestinationMeshletsCounter = Shader.PropertyToID(nameof(_DestinationMeshletsCounter));
                public static int _DestinationMeshlets = Shader.PropertyToID(nameof(_DestinationMeshlets));
                public static int _RendererListMeshletCounts = Shader.PropertyToID(nameof(_RendererListMeshletCounts));
            }

            public static class FixupMeshletCullingIndirectDispatchArgs
            {
                public static int _CullingContexts = Shader.PropertyToID(nameof(_CullingContexts));
                public static int _CullingContextCount = Shader.PropertyToID(nameof(_CullingContextCount));

                public static int _RequestCounters = Shader.PropertyToID(nameof(_RequestCounters));
                public static int _IndirectArgs = Shader.PropertyToID(nameof(_IndirectArgs));

                public static int _IndirectDrawArgsOffset = Shader.PropertyToID(nameof(_IndirectDrawArgsOffset));
                public static int _RendererListMeshletCounts = Shader.PropertyToID(nameof(_RendererListMeshletCounts));
                public static int _IndirectDrawArgs = Shader.PropertyToID(nameof(_IndirectDrawArgs));
            }

            public static class MeshletCulling
            {
                public static int _CullingContexts = Shader.PropertyToID(nameof(_CullingContexts));

                public static int _SourceMeshletsCounters = Shader.PropertyToID(nameof(_SourceMeshletsCounters));
                public static int _SourceMeshlets = Shader.PropertyToID(nameof(_SourceMeshlets));

                public static int _IndirectDrawArgs = Shader.PropertyToID(nameof(_IndirectDrawArgs));
                public static int _IndirectDrawArgsOffset = Shader.PropertyToID(nameof(_IndirectDrawArgsOffset));
                public static int _DestinationMeshlets = Shader.PropertyToID(nameof(_DestinationMeshlets));
            }
        }
    }
}