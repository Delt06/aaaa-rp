using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.Core.ObjectDispatching;
using DELTation.AAAARP.Data;
using DELTation.AAAARP.Debugging;
using DELTation.AAAARP.Meshlets;
using DELTation.AAAARP.Passes;
using DELTation.AAAARP.RenderPipelineResources;
using DELTation.AAAARP.Utils;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Renderers
{
    public class AAAARendererContainer : IDisposable
    {
        public enum PassType
        {
            Visibility = 0,
            ShadowCaster = 1,
        }

        private readonly BindlessTextureContainer _bindlessTextureContainer;

        [CanBeNull]
        private readonly AAAARenderPipelineDebugDisplaySettings _debugDisplaySettings;
        private readonly MaterialDataBuffer _materialDataBuffer;
        private readonly MaterialPropertyBlock _materialPropertyBlock = new();
        private readonly Dictionary<int, MeshMetadata> _meshInstanceIDToMetadata = new();
        private readonly AAAAMeshLODSettings _meshLODSettings;

        private readonly AAAAObjectTracker _objectTracker;
        private readonly RendererList[] _rendererLists;
        private bool _isDirty;

        private NativeList<AAAAMeshlet> _meshletData;
        private GraphicsBuffer _meshletsDataBuffer;
        private NativeList<AAAAMeshLODNode> _meshLODNodes;
        private GraphicsBuffer _meshLODNodesBuffer;
        private GraphicsBuffer _sharedIndexBuffer;
        private NativeList<byte> _sharedIndices;
        private GraphicsBuffer _sharedVertexBuffer;
        private NativeList<AAAAMeshletVertex> _sharedVertices;

        internal AAAARendererContainer(BindlessTextureContainer bindlessTextureContainer, AAAAMeshLODSettings meshLODSettings,
            AAAARawBufferClear rawBufferClear,
            [CanBeNull] AAAARenderPipelineDebugDisplaySettings debugDisplaySettings)
        {
            _bindlessTextureContainer = bindlessTextureContainer;
            AAAARenderPipelineRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAARenderPipelineRuntimeShaders>();

            _meshLODSettings = meshLODSettings;
            _debugDisplaySettings = debugDisplaySettings;
            _materialDataBuffer = new MaterialDataBuffer(_bindlessTextureContainer, Allocator.Persistent);
            InstanceDataBuffer = new InstanceDataBuffer(this, _materialDataBuffer, Allocator.Persistent);
            OcclusionCullingResources = new OcclusionCullingResources(rawBufferClear);
            _meshLODNodes = new NativeList<AAAAMeshLODNode>(Allocator.Persistent);
            _meshletData = new NativeList<AAAAMeshlet>(Allocator.Persistent);
            _sharedVertices = new NativeList<AAAAMeshletVertex>(Allocator.Persistent);
            _sharedIndices = new NativeList<byte>(Allocator.Persistent);

            _objectTracker = new AAAAObjectTracker(InstanceDataBuffer, _materialDataBuffer, _bindlessTextureContainer);

            _rendererLists = new RendererList[(int) AAAARendererListID.Count];

            for (int listIndex = 0; listIndex < _rendererLists.Length; listIndex++)
            {
                var listID = (AAAARendererListID) listIndex;
                _rendererLists[listIndex] = CreateRendererList(listID, shaders);
            }

#if UNITY_EDITOR

            // Make sure all the instances are ready for the very first frame
            ObjectDispatcherService.ProcessUpdates();

#endif

            _isDirty = true;
        }

        public int MeshletRenderRequestByteStridePerContext { get; private set; }
        public int IndirectDrawArgsByteStridePerContext { get; private set; }

        internal OcclusionCullingResources OcclusionCullingResources { get; }

        public int MaxMeshletListBuildJobCount { get; internal set; }

        public int MeshLODNodeCount => _meshLODNodes.Length;

        internal InstanceDataBuffer InstanceDataBuffer { get; }
        public GraphicsBuffer IndirectDrawArgsBuffer { get; private set; }
        public GraphicsBuffer MeshletRenderRequestsBuffer { get; private set; }

        private int MaxMeshLODLevelsCount { get; set; }

        public int MaxMeshletRenderRequestsPerList { get; private set; }
        public int RendererListCount => _rendererLists.Length;

        public void Dispose()
        {
            OcclusionCullingResources.Dispose();
            _objectTracker.Dispose();

            if (_meshLODNodes.IsCreated)
            {
                _meshLODNodes.Dispose();
            }

            if (_meshletData.IsCreated)
            {
                _meshletData.Dispose();
            }

            InstanceDataBuffer?.Dispose();
            _materialDataBuffer?.Dispose();

            if (_sharedVertices.IsCreated)
            {
                _sharedVertices.Dispose();
            }

            if (_sharedIndices.IsCreated)
            {
                _sharedIndices.Dispose();
            }

            IndirectDrawArgsBuffer?.Dispose();
            _meshLODNodesBuffer?.Dispose();
            _meshletsDataBuffer?.Dispose();
            _sharedVertexBuffer?.Dispose();
            _sharedIndexBuffer?.Dispose();
            MeshletRenderRequestsBuffer?.Dispose();

            foreach (RendererList rendererList in _rendererLists)
            {
                CoreUtils.Destroy(rendererList.Material);
            }
        }

        private static RendererList CreateRendererList(AAAARendererListID listID, AAAARenderPipelineRuntimeShaders shaders)
        {
            Shader shader = shaders.VisibilityBufferPS;

            RendererList rendererList;
            rendererList.Material = CoreUtils.CreateEngineMaterial(shader);
            rendererList.Material.SetKeyword(new LocalKeyword(shader, "_ALPHATEST_ON"), (listID & AAAARendererListID.AlphaTest) != 0);

            CullMode cullMode = (listID & AAAARendererListID.CullFront) != 0 ? CullMode.Front : CullMode.Back;
            cullMode = (listID & AAAARendererListID.CullOff) != 0 ? CullMode.Off : cullMode;
            rendererList.Material.SetFloat(ShaderIDs._Cull, (float) cullMode);

            return rendererList;
        }

        public void PreRender(ScriptableRenderContext context)
        {
            _bindlessTextureContainer.PreRender();

            if (_isDirty)
            {
                UploadData();
                _isDirty = false;
            }

            CommandBuffer cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, Profiling.PreRender))
            {
                InstanceDataBuffer.PreRender(cmd);
                _materialDataBuffer.PreRender(cmd);
                OcclusionCullingResources.PreRender(cmd);

                cmd.SetGlobalBuffer(ShaderIDs._Meshlets, _meshletsDataBuffer);
                cmd.SetGlobalBuffer(ShaderIDs._MeshLODNodes, _meshLODNodesBuffer);
                cmd.SetGlobalInt(ShaderIDs._ForcedMeshLODNodeDepth, GetForcedMeshLODNodeDepth());
                cmd.SetGlobalFloat(ShaderIDs._MeshLODErrorThreshold, GetMeshLODErrorThreshold());
                cmd.SetGlobalBuffer(ShaderIDs._SharedVertexBuffer, _sharedVertexBuffer);
                cmd.SetGlobalBuffer(ShaderIDs._SharedIndexBuffer, _sharedIndexBuffer);
                cmd.SetGlobalBuffer(ShaderIDs._MeshletRenderRequests, MeshletRenderRequestsBuffer);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void PostRender()
        {
            using (new ProfilingScope(Profiling.PostRender))
            {
                OcclusionCullingResources.PostRender();
            }
        }

        private void UploadData()
        {
            if (InstanceDataBuffer.InstanceCount == 0)
            {
                return;
            }

            MaxMeshletRenderRequestsPerList = FindMaxSimultaneousMeshletCount();

            _meshLODNodesBuffer?.Dispose();
            _meshLODNodesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, math.max(1, _meshLODNodes.Length),
                UnsafeUtility.SizeOf<AAAAMeshLODNode>()
            )
            {
                name = "MeshLODNodes",
            };
            _meshLODNodesBuffer.SetData(_meshLODNodes.AsArray());

            _meshletsDataBuffer?.Dispose();
            _meshletsDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                _meshletData.Length, UnsafeUtility.SizeOf<AAAAMeshlet>()
            )
            {
                name = "MeshletsData",
            };
            _meshletsDataBuffer.SetData(_meshletData.AsArray());

            _sharedVertexBuffer?.Dispose();
            _sharedVertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                _sharedVertices.Length, UnsafeUtility.SizeOf<AAAAMeshletVertex>()
            );
            _sharedVertexBuffer.SetData(_sharedVertices.AsArray());

            while (_sharedIndices.Length % 4 > 0)
            {
                _sharedIndices.Add(0);
            }
            _sharedIndexBuffer?.Dispose();
            _sharedIndexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw,
                AAAAMathUtils.AlignUp(_sharedIndices.Length, sizeof(uint)) / sizeof(uint), sizeof(uint)
            );
            _sharedIndexBuffer.SetData(_sharedIndices.AsArray());

            MeshletRenderRequestByteStridePerContext = AAAAMathUtils.AlignUp(
                _rendererLists.Length * math.max(1, MaxMeshletRenderRequestsPerList) * UnsafeUtility.SizeOf<AAAAMeshletRenderRequestPacked>(),
                sizeof(uint)
            );
            MeshletRenderRequestsBuffer?.Dispose();
            MeshletRenderRequestsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw,
                GPUCullingContext.MaxCullingContextsPerBatch * MeshletRenderRequestByteStridePerContext / sizeof(uint), sizeof(uint)
            )
            {
                name = "VisibilityBuffer_MeshletRenderRequests",
            };

            IndirectDrawArgsByteStridePerContext = _rendererLists.Length * GraphicsBuffer.IndirectDrawArgs.size;
            IndirectDrawArgsBuffer?.Dispose();
            IndirectDrawArgsBuffer =
                new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                    GPUCullingContext.MaxCullingContextsPerBatch * IndirectDrawArgsByteStridePerContext / sizeof(uint),
                    sizeof(uint)
                )
                {
                    name = "VisibilityBuffer_IndirectDrawArgs",
                };
        }

        private int FindMaxSimultaneousMeshletCount()
        {
            var instanceMetadata = new NativeList<InstanceDataBuffer.InstanceMetadata>(InstanceDataBuffer.InstanceCount, Allocator.Temp);
            InstanceDataBuffer.GetInstanceMetadata(instanceMetadata);

            int meshletCount = 0;

            foreach (InstanceDataBuffer.InstanceMetadata instance in instanceMetadata)
            {
                if (_meshInstanceIDToMetadata.TryGetValue(instance.MeshInstanceID, out MeshMetadata meshMetadata))
                {
                    meshletCount += meshMetadata.LeafMeshletCount;
                }
            }

            return meshletCount;
        }

        public void Draw(CameraType cameraType, CommandBuffer cmd, PassType passType, int contextIndex)
        {
            if (!ShouldDraw(cameraType))
            {
                return;
            }

            if (IndirectDrawArgsBuffer != null && InstanceDataBuffer.InstanceCount > 0)
            {
                int baseCommandID = contextIndex * _rendererLists.Length;
                int baseArgsOffset = contextIndex * IndirectDrawArgsByteStridePerContext;

                for (int index = 0; index < _rendererLists.Length; index++)
                {
                    ref readonly RendererList rendererList = ref _rendererLists[index];
                    int commandID = baseCommandID + index;
                    int argsOffset = baseArgsOffset + index * GraphicsBuffer.IndirectDrawArgs.size;

                    _materialPropertyBlock.Clear();
                    _materialPropertyBlock.SetBuffer(ShaderIDs.unity_IndirectDrawArgs, IndirectDrawArgsBuffer);
                    _materialPropertyBlock.SetInteger(ShaderIDs.unity_BaseCommandID, commandID);
                    cmd.DrawProceduralIndirect(Matrix4x4.identity, rendererList.Material, (int) passType, MeshTopology.Triangles,
                        IndirectDrawArgsBuffer, argsOffset, _materialPropertyBlock
                    );
                }
            }
        }

        private static bool ShouldDraw(CameraType cameraType) => cameraType is CameraType.Game or CameraType.SceneView;

        private int GetForcedMeshLODNodeDepth() => _debugDisplaySettings?.RenderingSettings.ForcedMeshLODNodeDepth ?? -1;

        private float GetMeshLODErrorThreshold() =>
            math.max(0, _meshLODSettings.ErrorThreshold + (_debugDisplaySettings?.RenderingSettings.MeshLODErrorThresholdBias ?? 0.0f));

        internal MeshMetadata GetOrAllocateMeshLODNodes(AAAAMeshletCollectionAsset meshletCollection)
        {
            int meshInstanceID = meshletCollection.GetInstanceID();

            if (_meshInstanceIDToMetadata.TryGetValue(meshInstanceID, out MeshMetadata meshMetadata))
            {
                return meshMetadata;
            }

            MaxMeshLODLevelsCount = Mathf.Max(MaxMeshLODLevelsCount, meshletCollection.MeshLODLevelCount);

            uint triangleOffset = (uint) _sharedIndices.Length;
            uint vertexOffset = (uint) _sharedVertices.Length;
            uint meshletOffset = (uint) _meshletData.Length;
            meshMetadata = new MeshMetadata
            {
                TopMeshLODNodesStartIndex = _meshLODNodes.Length,
                LeafMeshletCount = meshletCollection.LeafMeshletCount,
            };

            foreach (AAAAMeshlet sourceMeshlet in meshletCollection.Meshlets)
            {
                AAAAMeshlet meshlet = sourceMeshlet;
                meshlet.TriangleOffset += triangleOffset;
                meshlet.VertexOffset += vertexOffset;

                _meshletData.Add(meshlet);
            }

            foreach (AAAAMeshLODNode sourceNode in meshletCollection.MeshLODNodes)
            {
                AAAAMeshLODNode node = sourceNode;
                node.MeshletStartIndex += meshletOffset;

                _meshLODNodes.Add(node);
            }

            AppendFromManagedArray(_sharedVertices, meshletCollection.VertexBuffer);
            AppendFromManagedArray(_sharedIndices, meshletCollection.IndexBuffer);

            _meshInstanceIDToMetadata.Add(meshInstanceID, meshMetadata);
            _isDirty = true;
            return meshMetadata;
        }

        private static unsafe void AppendFromManagedArray<T>(NativeList<T> destination, T[] source) where T : unmanaged
        {
            int offset = destination.Length;

            destination.Resize(offset + source.Length, NativeArrayOptions.UninitializedMemory);
            fixed (T* pSource = source)
            {
                UnsafeUtility.MemCpy(destination.GetUnsafePtr() + offset, pSource, source.Length * UnsafeUtility.SizeOf<T>());
            }
        }

        internal struct MeshMetadata
        {
            public int TopMeshLODNodesStartIndex;
            public int LeafMeshletCount;
        }

        public struct RendererList
        {
            public Material Material;
        }

        private static class Profiling
        {
            public static readonly ProfilingSampler PreRender = new("Visibility Buffer Container: Pre Render");
            public static readonly ProfilingSampler PostRender = new("Visibility Buffer Container: Post Render");
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static readonly int _Meshlets = Shader.PropertyToID(nameof(_Meshlets));
            public static readonly int _MeshLODNodes = Shader.PropertyToID(nameof(_MeshLODNodes));
            public static readonly int _ForcedMeshLODNodeDepth = Shader.PropertyToID(nameof(_ForcedMeshLODNodeDepth));
            public static readonly int _MeshLODErrorThreshold = Shader.PropertyToID(nameof(_MeshLODErrorThreshold));
            public static readonly int _SharedVertexBuffer = Shader.PropertyToID(nameof(_SharedVertexBuffer));
            public static readonly int _SharedIndexBuffer = Shader.PropertyToID(nameof(_SharedIndexBuffer));
            public static readonly int _MeshletRenderRequests = Shader.PropertyToID(nameof(_MeshletRenderRequests));
            public static readonly int unity_IndirectDrawArgs = Shader.PropertyToID(nameof(unity_IndirectDrawArgs));
            public static readonly int unity_BaseCommandID = Shader.PropertyToID(nameof(unity_BaseCommandID));
            public static readonly int _Cull = Shader.PropertyToID(nameof(_Cull));
        }
    }
}