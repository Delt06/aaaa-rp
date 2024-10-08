using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.Core.ObjectDispatching;
using DELTation.AAAARP.Data;
using DELTation.AAAARP.Debugging;
using DELTation.AAAARP.Materials;
using DELTation.AAAARP.Meshlets;
using DELTation.AAAARP.RenderPipelineResources;
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
        private readonly Material _material;
        private readonly Dictionary<AAAAMaterialAsset, int> _materialToIndex = new();
        private readonly Dictionary<AAAAMeshletCollectionAsset, int> _meshletCollectionToTopMeshLODNodesStartIndex = new();
        private readonly AAAAMeshLODSettings _meshLODSettings;

        private readonly AAAAObjectTracker _objectTracker;
        private bool _isDirty;

        private NativeList<AAAAMaterialData> _materialData;
        private GraphicsBuffer _materialDataBuffer;
        private NativeList<AAAAMeshlet> _meshletData;
        private GraphicsBuffer _meshletsDataBuffer;
        private NativeList<AAAAMeshLODNode> _meshLODNodes;
        private GraphicsBuffer _meshLODNodesBuffer;
        private GraphicsBuffer _sharedIndexBuffer;
        private NativeList<byte> _sharedIndices;
        private GraphicsBuffer _sharedVertexBuffer;
        private NativeList<AAAAMeshletVertex> _sharedVertices;

        internal AAAARendererContainer(BindlessTextureContainer bindlessTextureContainer, AAAAMeshLODSettings meshLODSettings,
            [CanBeNull] AAAARenderPipelineDebugDisplaySettings debugDisplaySettings)
        {
            _bindlessTextureContainer = bindlessTextureContainer;
            AAAARenderPipelineRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAARenderPipelineRuntimeShaders>();

            _meshLODSettings = meshLODSettings;
            _debugDisplaySettings = debugDisplaySettings;
            InstanceDataBuffer = new InstanceDataBuffer(this, Allocator.Persistent);
            OcclusionCullingResources = new OcclusionCullingResources(shaders.RawBufferClearCS);
            _meshLODNodes = new NativeList<AAAAMeshLODNode>(Allocator.Persistent);
            _meshletData = new NativeList<AAAAMeshlet>(Allocator.Persistent);
            _materialData = new NativeList<AAAAMaterialData>(Allocator.Persistent);
            _sharedVertices = new NativeList<AAAAMeshletVertex>(Allocator.Persistent);
            _sharedIndices = new NativeList<byte>(Allocator.Persistent);

            _objectTracker = new AAAAObjectTracker(InstanceDataBuffer, _bindlessTextureContainer);

            _material = CoreUtils.CreateEngineMaterial(shaders.VisibilityBufferPS);

#if UNITY_EDITOR

            // Make sure all the instances are ready for the very first frame
            ObjectDispatcherService.ProcessUpdates();

#endif

            _isDirty = true;
        }

        internal OcclusionCullingResources OcclusionCullingResources { get; }

        public int MaxMeshletListBuildJobCount { get; internal set; }

        public int MeshLODNodeCount => _meshLODNodes.Length;

        internal InstanceDataBuffer InstanceDataBuffer { get; }
        public GraphicsBuffer IndirectDrawArgsBuffer { get; private set; }
        public GraphicsBuffer MeshletRenderRequestsBuffer { get; private set; }

        private int MaxMeshLODLevelsCount { get; set; }

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

            InstanceDataBuffer.Dispose();

            if (_materialData.IsCreated)
            {
                _materialData.Dispose();
            }

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
            InstanceDataBuffer?.Dispose();
            _materialDataBuffer?.Dispose();
            MeshletRenderRequestsBuffer?.Dispose();
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
                OcclusionCullingResources.PreRender(cmd);

                cmd.SetGlobalBuffer(ShaderIDs._Meshlets, _meshletsDataBuffer);
                cmd.SetGlobalBuffer(ShaderIDs._MeshLODNodes, _meshLODNodesBuffer);
                cmd.SetGlobalInt(ShaderIDs._ForcedMeshLODNodeDepth, GetForcedMeshLODNodeDepth());
                cmd.SetGlobalFloat(ShaderIDs._MeshLODErrorThreshold, GetMeshLODErrorThreshold());
                cmd.SetGlobalBuffer(ShaderIDs._SharedVertexBuffer, _sharedVertexBuffer);
                cmd.SetGlobalBuffer(ShaderIDs._SharedIndexBuffer, _sharedIndexBuffer);
                cmd.SetGlobalBuffer(ShaderIDs._MaterialData, _materialDataBuffer);
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

            _materialDataBuffer?.Dispose();
            _materialDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                _materialData.Length, UnsafeUtility.SizeOf<AAAAMaterialData>()
            )
            {
                name = "MaterialData",
            };
            _materialDataBuffer.SetData(_materialData.AsArray());

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

            MeshletRenderRequestsBuffer?.Dispose();
            MeshletRenderRequestsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw,
                AAAAMathUtils.AlignUp(4 * _meshletData.Length * UnsafeUtility.SizeOf<AAAAMeshletRenderRequestPacked>(), sizeof(uint)) / sizeof(uint),
                sizeof(uint)
            )
            {
                name = "VisibilityBuffer_MeshletRenderRequests",
            };

            IndirectDrawArgsBuffer?.Dispose();
            IndirectDrawArgsBuffer =
                new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw, GraphicsBuffer.IndirectDrawArgs.size / sizeof(uint),
                    sizeof(uint)
                )
                {
                    name = "VisibilityBuffer_IndirectDrawArgs",
                };
        }

        public void Draw(CameraType cameraType, CommandBuffer cmd, PassType passType)
        {
            if (!ShouldDraw(cameraType))
            {
                return;
            }

            if (IndirectDrawArgsBuffer != null && InstanceDataBuffer.InstanceCount > 0)
            {
                const int argsOffset = 0;
                cmd.DrawProceduralIndirect(Matrix4x4.identity, _material, (int) passType, MeshTopology.Triangles, IndirectDrawArgsBuffer, argsOffset);
            }
        }

        private static bool ShouldDraw(CameraType cameraType) => cameraType is CameraType.Game or CameraType.SceneView;

        private int GetForcedMeshLODNodeDepth() => _debugDisplaySettings?.RenderingSettings.ForcedMeshLODNodeDepth ?? -1;

        private float GetMeshLODErrorThreshold() =>
            math.max(0, _meshLODSettings.ErrorThreshold + (_debugDisplaySettings?.RenderingSettings.MeshLODErrorThresholdBias ?? 0.0f));

        internal int GetOrAllocateMeshLODNodes(AAAAMeshletCollectionAsset meshletCollection)
        {
            if (_meshletCollectionToTopMeshLODNodesStartIndex.TryGetValue(meshletCollection, out int meshLODNodeStartIndex))
            {
                return meshLODNodeStartIndex;
            }

            MaxMeshLODLevelsCount = Mathf.Max(MaxMeshLODLevelsCount, meshletCollection.MeshLODLevelCount);

            uint triangleOffset = (uint) _sharedIndices.Length;
            uint vertexOffset = (uint) _sharedVertices.Length;
            uint meshletOffset = (uint) _meshletData.Length;
            meshLODNodeStartIndex = _meshLODNodes.Length;

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

            _meshletCollectionToTopMeshLODNodesStartIndex.Add(meshletCollection, meshLODNodeStartIndex);
            _isDirty = true;
            return meshLODNodeStartIndex;
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

        internal int GetOrAllocateMaterial(AAAAMaterialAsset material)
        {
            if (_materialToIndex.TryGetValue(material, out int index))
            {
                return index;
            }

            var materialData = new AAAAMaterialData
            {
                AlbedoColor = (Vector4) material.AlbedoColor,
                AlbedoIndex = GetOrAllocateTexture(material.Albedo),
                TextureTilingOffset = material.TextureTilingOffset,

                NormalsIndex = GetOrAllocateTexture(material.Normals),
                NormalsStrength = material.NormalsStrength,

                MasksIndex = GetOrAllocateTexture(material.Masks),
                Roughness = material.Roughness,
                Metallic = material.Metallic,
            };
            _materialData.Add(materialData);
            index = _materialData.Length - 1;
            _materialToIndex.Add(material, index);
            _isDirty = true;
            return index;
        }

        private uint GetOrAllocateTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return AAAAMaterialData.NoTextureIndex;
            }

            return _bindlessTextureContainer.GetOrCreateIndex(texture, texture.GetInstanceID());
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
            public static readonly int _MaterialData = Shader.PropertyToID(nameof(_MaterialData));
            public static readonly int _MeshletRenderRequests = Shader.PropertyToID(nameof(_MeshletRenderRequests));
        }
    }
}