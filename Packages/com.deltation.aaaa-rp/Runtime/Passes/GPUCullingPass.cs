using System;
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
            [CanBeNull] AAAARenderPipelineDebugDisplaySettings debugDisplaySettings, string nameTag = null) : base(renderPassEvent)
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
            passData.MaxMeshletRenderRequestsPerList = rendererContainer.MaxMeshletRenderRequestsPerList;
            passData.RendererListCount = rendererContainer.RendererListCount;

            if (passData.InstanceCount == 0)
            {
                return;
            }

            {
                AAAACameraData cameraData = frameData.Get<AAAACameraData>();
                Camera camera = CullingCameraOverride != null ? CullingCameraOverride : cameraData.Camera;
                Transform cameraTransform = camera.transform;
                Vector3 cameraPosition = cameraTransform.position;

                Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
                Matrix4x4 viewProjectionMatrix = camera.projectionMatrix * viewMatrix;
                Matrix4x4 gpuViewProjectionMatrix = GL.GetGPUProjectionMatrix(viewProjectionMatrix, true);

                var pixelSize = new Vector2(cameraData.ScaledWidth, cameraData.ScaledHeight);
                Vector3 cameraRight = cameraTransform.right;
                Vector3 cameraUp = cameraTransform.up;

                if (CullingViewParametersOverride != null)
                {
                    passData.CullingView = CullingViewParametersOverride.Value;
                }
                else
                {
                    passData.CullingView = new CullingViewParameters
                    {
                        ViewMatrix = viewMatrix,
                        ViewProjectionMatrix = viewProjectionMatrix,
                        GPUViewProjectionMatrix = gpuViewProjectionMatrix,
                        CameraPosition = cameraPosition,
                        CameraRight = cameraRight,
                        CameraUp = cameraUp,
                        PixelSize = pixelSize,
                        IsPerspective = !camera.orthographic,
                        BoundingSphereWS = math.float4(0, 0, 0, 0),
                        PassMask = AAAAInstancePassMask.Main,
                        DisableOcclusionCulling = CullingCameraOverride != null,
                    };
                }

                // LOD for shadows should be the same as for main view.
                // We do not explicitly synchronize LOD selection, just the input parameters.
                passData.LODSelectionContext = new LODSelectionContext
                {
                    CameraPosition = cameraPosition,
                    CameraRight = cameraRight,
                    CameraUp = cameraUp,
                    PixelSize = pixelSize,
                    GPUViewProjectionMatrix = gpuViewProjectionMatrix,
                };
            }

            Plane[] frustumPlanes = TempCollections.Planes;
            GeometryUtility.CalculateFrustumPlanes(passData.CullingView.ViewProjectionMatrix, frustumPlanes);

            for (int i = 0; i < frustumPlanes.Length; i++)
            {
                Plane frustumPlane = frustumPlanes[i];
                passData.FrustumPlanes[i] = new Vector4(frustumPlane.normal.x, frustumPlane.normal.y, frustumPlane.normal.z, frustumPlane.distance);
            }

            passData.CullingSphereLS = float4.zero;
            if (passData.CullingView.BoundingSphereWS.w > 0.0f)
            {
                passData.CullingSphereLS.xyz = math.transform(passData.CullingView.ViewMatrix, passData.CullingView.BoundingSphereWS.xyz);
                passData.CullingSphereLS.w = passData.CullingView.BoundingSphereWS.w;
            }

            passData.InstanceIndices = builder.CreateTransientBuffer(
                new BufferDesc(_instanceIndices.Length, sizeof(uint), GraphicsBuffer.Target.Raw)
                {
                    name = nameof(PassData.InstanceIndices),
                }
            );

            GraphicsBuffer meshletRenderRequestsBuffer = rendererContainer.MeshletRenderRequestsBuffer;

            passData.InitialMeshletListCounterBuffer =
                builder.CreateTransientBuffer(CreateCounterBufferDesc("InitialMeshletListCounter", extraTargets: GraphicsBuffer.Target.CopyDestination));
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

            passData.DestinationMeshletsBuffer = builder.WriteBuffer(renderingData.RenderGraph.ImportBuffer(meshletRenderRequestsBuffer));

            passData.IndirectDrawArgsBuffer = builder.WriteBuffer(renderingData.RenderGraph.ImportBuffer(rendererContainer.IndirectDrawArgsBuffer));

            if (_passType == PassType.FalseNegative)
            {
                AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
                builder.ReadTexture(resourceData.CameraScaledDepthBuffer);
                builder.ReadTexture(resourceData.CameraHZBScaled);
            }

            passData.DebugDataBuffer = _debugDisplaySettings is { RenderingSettings: { DebugGPUCulling: true } } && CullingViewParametersOverride == null
                ? builder.ReadBuffer(frameData.Get<AAAADebugData>().GPUCullingDebugBuffer)
                : default;
        }

        private static BufferDesc CreateCounterBufferDesc(string name, int count = 1, GraphicsBuffer.Target extraTargets = default) =>
            new(count, sizeof(uint), GraphicsBuffer.Target.Raw | extraTargets)
            {
                name = name,
            };

        protected override void Render(PassData data, RenderGraphContext context)
        {
            if (data.InstanceCount == 0)
            {
                return;
            }

            using (new ProfilingScope(context.cmd, Profiling.InitBuffers))
            {
                _rawBufferClear.FastZeroClear(context.cmd, data.InitialMeshletListCounterBuffer, 1);

                context.cmd.SetBufferData(data.MeshletListBuildIndirectDispatchArgsBuffer, new NativeArray<IndirectDispatchArgs>(1, Allocator.Temp)
                    {
                        [0] = new IndirectDispatchArgs
                        {
                            ThreadGroupsX = 0,
                            ThreadGroupsY = 1,
                            ThreadGroupsZ = 1,
                        },
                    }
                );

                var initialIndirectDrawArgs =
                    new NativeArray<GraphicsBuffer.IndirectDrawArgs>(data.RendererListCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int rendererListIndex = 0; rendererListIndex < data.RendererListCount; rendererListIndex++)
                {
                    initialIndirectDrawArgs[rendererListIndex] = new GraphicsBuffer.IndirectDrawArgs
                    {
                        startInstance = (uint) (rendererListIndex * data.MaxMeshletRenderRequestsPerList),
                        instanceCount = 0,
                        startVertex = 0,
                        vertexCountPerInstance = AAAAMeshletConfiguration.MaxMeshletIndices,
                    };
                }
                context.cmd.SetBufferData(data.IndirectDrawArgsBuffer, initialIndirectDrawArgs);

                if (data.OcclusionCullingInstanceVisibilityMask.IsValid())
                {
                    _rawBufferClear.DispatchClear(context.cmd, data.OcclusionCullingInstanceVisibilityMask,
                        data.OcclusionCullingInstanceVisibilityMaskCount, 0, 0
                    );
                }
            }

            ref readonly CullingViewParameters view = ref data.CullingView;

            using (new ProfilingScope(context.cmd, Profiling.InstanceCulling))
            {
                const int kernelIndex = 0;

                CoreUtils.SetKeyword(context.cmd, _gpuInstanceCullingCS, Keywords.MAIN_PASS, _passType == PassType.Main);
                CoreUtils.SetKeyword(context.cmd, _gpuInstanceCullingCS, Keywords.FALSE_NEGATIVE_PASS, _passType == PassType.FalseNegative);
                CoreUtils.SetKeyword(context.cmd, _gpuInstanceCullingCS, Keywords.DISABLE_OCCLUSION_CULLING, view.DisableOcclusionCulling);
                CoreUtils.SetKeyword(context.cmd, _gpuInstanceCullingCS, Keywords.Debug.DEBUG_GPU_CULLING, data.DebugDataBuffer.IsValid());

                context.cmd.SetComputeVectorArrayParam(_gpuInstanceCullingCS, ShaderID.GPUInstanceCulling._CameraFrustumPlanes, data.FrustumPlanes);
                context.cmd.SetComputeVectorParam(_gpuInstanceCullingCS, ShaderID.GPUInstanceCulling._CullingSphereLS, data.CullingSphereLS);
                context.cmd.SetComputeMatrixParam(_gpuInstanceCullingCS, ShaderID.GPUInstanceCulling._CameraViewProjection, view.GPUViewProjectionMatrix);
                context.cmd.SetComputeMatrixParam(_gpuInstanceCullingCS, ShaderID.GPUInstanceCulling._CameraView, view.ViewMatrix);
                context.cmd.SetComputeIntParam(_gpuInstanceCullingCS, ShaderID.GPUInstanceCulling._PassMask, (int) view.PassMask);

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

                // Meshlet list build indirect args ThreadGroupsX is the same as job counter.
                // Bind it directly to avoid a separate dispatch for indirect args fixup.
                context.cmd.SetComputeBufferParam(_gpuInstanceCullingCS, kernelIndex,
                    ShaderID.GPUInstanceCulling._JobCounter, data.MeshletListBuildIndirectDispatchArgsBuffer
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

            using (new ProfilingScope(context.cmd, Profiling.MeshletListBuild))
            {
                const int kernelIndex = 0;

                ref readonly LODSelectionContext lodContext = ref data.LODSelectionContext;
                context.cmd.SetComputeMatrixParam(_meshletListBuildCS, ShaderID.MeshletListBuild._CameraViewProjection, lodContext.GPUViewProjectionMatrix);
                context.cmd.SetComputeVectorParam(_meshletListBuildCS, ShaderID.MeshletListBuild._CameraPosition, lodContext.CameraPosition);
                context.cmd.SetComputeVectorParam(_meshletListBuildCS, ShaderID.MeshletListBuild._CameraUp, lodContext.CameraUp);
                context.cmd.SetComputeVectorParam(_meshletListBuildCS, ShaderID.MeshletListBuild._CameraRight, lodContext.CameraRight);
                context.cmd.SetComputeVectorParam(_meshletListBuildCS, ShaderID.MeshletListBuild._ScreenSizePixels, lodContext.PixelSize);

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

            using (new ProfilingScope(context.cmd, Profiling.MeshletCulling))
            {
                const int kernelIndex = 0;

                CoreUtils.SetKeyword(context.cmd, _gpuMeshletCullingCS, Keywords.MAIN_PASS, _passType == PassType.Main);
                CoreUtils.SetKeyword(context.cmd, _gpuMeshletCullingCS, Keywords.FALSE_NEGATIVE_PASS, _passType == PassType.FalseNegative);
                CoreUtils.SetKeyword(context.cmd, _gpuMeshletCullingCS, Keywords.DISABLE_OCCLUSION_CULLING, view.DisableOcclusionCulling);
                CoreUtils.SetKeyword(context.cmd, _gpuMeshletCullingCS, Keywords.Debug.DEBUG_GPU_CULLING, data.DebugDataBuffer.IsValid());

                context.cmd.SetComputeVectorArrayParam(_gpuMeshletCullingCS, ShaderID.MeshletCulling._CameraFrustumPlanes, data.FrustumPlanes);
                context.cmd.SetComputeVectorParam(_gpuMeshletCullingCS, ShaderID.MeshletCulling._CullingSphereLS, data.CullingSphereLS);
                context.cmd.SetComputeVectorParam(_gpuMeshletCullingCS, ShaderID.MeshletCulling._CameraPosition, view.CameraPosition);
                context.cmd.SetComputeMatrixParam(_gpuMeshletCullingCS, ShaderID.MeshletCulling._CameraViewProjection,
                    view.GPUViewProjectionMatrix
                );
                context.cmd.SetComputeMatrixParam(_gpuMeshletCullingCS, ShaderID.MeshletCulling._CameraView,
                    view.ViewMatrix
                );
                context.cmd.SetComputeFloatParam(_gpuMeshletCullingCS, ShaderID.MeshletCulling._CameraIsPerspective,
                    view.IsPerspective ? 1.0f : 0.0f
                );

                context.cmd.SetComputeBufferParam(_gpuMeshletCullingCS, kernelIndex,
                    ShaderID.MeshletCulling._SourceMeshletsCounter, data.InitialMeshletListCounterBuffer
                );
                context.cmd.SetComputeBufferParam(_gpuMeshletCullingCS, kernelIndex,
                    ShaderID.MeshletCulling._SourceMeshlets, data.InitialMeshletListBuffer
                );

                // Bind draw args directly, eliminating the need for a separate indirect args fixup dispatch.
                context.cmd.SetComputeBufferParam(_gpuMeshletCullingCS, kernelIndex,
                    ShaderID.MeshletCulling._DestinationMeshletsCounter, data.IndirectDrawArgsBuffer
                );

                // Counter is draw args instance count. 
                context.cmd.SetComputeIntParam(_gpuMeshletCullingCS,
                    ShaderID.MeshletCulling._DestinationMeshletsCounterOffset, sizeof(uint)
                );
                context.cmd.SetComputeIntParam(_gpuMeshletCullingCS,
                    ShaderID.MeshletCulling._DestinationMeshletsCounterStride, GraphicsBuffer.IndirectDrawArgs.size
                );

                context.cmd.SetComputeBufferParam(_gpuMeshletCullingCS, kernelIndex,
                    ShaderID.MeshletCulling._DestinationMeshlets, data.DestinationMeshletsBuffer
                );
                context.cmd.SetComputeIntParam(_gpuMeshletCullingCS,
                    ShaderID.MeshletCulling._MaxMeshletRenderRequestsPerList, data.MaxMeshletRenderRequestsPerList
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

        public struct CullingViewParameters
        {
            public Vector3 CameraPosition;
            public Vector3 CameraUp;
            public Vector3 CameraRight;
            public Vector2 PixelSize;
            public Matrix4x4 ViewProjectionMatrix;
            public Matrix4x4 ViewMatrix;
            public Matrix4x4 GPUViewProjectionMatrix;
            public float4 BoundingSphereWS;
            public bool IsPerspective;
            public AAAAInstancePassMask PassMask;
            public bool DisableOcclusionCulling;
        }

        public class PassData : PassDataBase
        {
            public readonly Vector4[] FrustumPlanes = new Vector4[6];
            public float4 CullingSphereLS;

            public CullingViewParameters CullingView;

            public BufferHandle DebugDataBuffer;

            public BufferHandle DestinationMeshletsBuffer;
            public BufferHandle GPUMeshletCullingIndirectDispatchArgsBuffer;
            public BufferHandle IndirectDrawArgsBuffer;

            public BufferHandle InitialMeshletListBuffer;
            public BufferHandle InitialMeshletListCounterBuffer;

            public int InstanceCount;

            public BufferHandle InstanceIndices;
            public LODSelectionContext LODSelectionContext;
            public int MaxMeshletRenderRequestsPerList;

            public BufferHandle MeshletListBuildIndirectDispatchArgsBuffer;
            public BufferHandle MeshletListBuildJobsBuffer;

            public BufferHandle OcclusionCullingInstanceVisibilityMask;
            public int OcclusionCullingInstanceVisibilityMaskCount;
            public int RendererListCount;
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
                public static int _CameraFrustumPlanes = Shader.PropertyToID(nameof(_CameraFrustumPlanes));
                public static int _CullingSphereLS = Shader.PropertyToID(nameof(_CullingSphereLS));
                public static int _CameraViewProjection = Shader.PropertyToID(nameof(_CameraViewProjection));
                public static int _CameraView = Shader.PropertyToID(nameof(_CameraView));
                public static int _PassMask = Shader.PropertyToID(nameof(_PassMask));

                public static int _InstanceIndices = Shader.PropertyToID(nameof(_InstanceIndices));
                public static int _InstanceIndicesCount = Shader.PropertyToID(nameof(_InstanceIndicesCount));

                public static int _Jobs = Shader.PropertyToID(nameof(_Jobs));
                public static int _JobCounter = Shader.PropertyToID(nameof(_JobCounter));
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
                public static int _CullingSphereLS = Shader.PropertyToID(nameof(_CullingSphereLS));
                public static int _CameraPosition = Shader.PropertyToID(nameof(_CameraPosition));
                public static int _CameraViewProjection = Shader.PropertyToID(nameof(_CameraViewProjection));
                public static int _CameraView = Shader.PropertyToID(nameof(_CameraView));
                public static int _CameraIsPerspective = Shader.PropertyToID(nameof(_CameraIsPerspective));

                public static int _SourceMeshletsCounter = Shader.PropertyToID(nameof(_SourceMeshletsCounter));
                public static int _SourceMeshlets = Shader.PropertyToID(nameof(_SourceMeshlets));

                public static int _DestinationMeshletsCounter = Shader.PropertyToID(nameof(_DestinationMeshletsCounter));
                public static int _DestinationMeshletsCounterOffset = Shader.PropertyToID(nameof(_DestinationMeshletsCounterOffset));
                public static int _DestinationMeshletsCounterStride = Shader.PropertyToID(nameof(_DestinationMeshletsCounterStride));
                public static int _DestinationMeshlets = Shader.PropertyToID(nameof(_DestinationMeshlets));
                public static int _MaxMeshletRenderRequestsPerList = Shader.PropertyToID(nameof(_MaxMeshletRenderRequestsPerList));
            }
        }
    }
}