using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.Data;
using DELTation.AAAARP.Debugging;
using DELTation.AAAARP.Materials;
using DELTation.AAAARP.Meshlets;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace DELTation.AAAARP
{
    public class AAAAVisibilityBufferContainer : IDisposable
    {
        private readonly List<Texture2D> _albedoTextures = new();

        [CanBeNull]
        private readonly AAAARenderPipelineDebugDisplaySettings _debugDisplaySettings;

        private readonly GraphicsBuffer _instanceDataBuffer;
        private readonly Material _material;
        private readonly GraphicsBuffer _materialDataBuffer;
        private readonly Dictionary<AAAAMaterialAsset, int> _materialToIndex = new();
        private readonly Dictionary<AAAAMeshletCollectionAsset, int> _meshletCollectionToTopMeshLODNodesStartIndex = new();
        private readonly GraphicsBuffer _meshletsDataBuffer;
        private readonly GraphicsBuffer _meshLODNodesBuffer;
        private readonly AAAAMeshLODSettings _meshLODSettings;
        private readonly GraphicsBuffer _sharedIndexBuffer;
        private readonly GraphicsBuffer _sharedVertexBuffer;
        private readonly Dictionary<Texture2D, int> _textureToAlbedoIndex = new();
        private NativeList<AAAAInstanceData> _instanceData;
        private NativeList<AAAAMaterialData> _materialData;
        private NativeList<AAAAMeshlet> _meshletData;
        private NativeList<AAAAMeshLODNode> _meshLODNodes;
        private NativeList<byte> _sharedIndices;
        private NativeList<AAAAMeshletVertex> _sharedVertices;

        internal AAAAVisibilityBufferContainer(AAAAMeshLODSettings meshLODSettings, [CanBeNull] AAAARenderPipelineDebugDisplaySettings debugDisplaySettings)
        {
            _meshLODSettings = meshLODSettings;
            _debugDisplaySettings = debugDisplaySettings;
            _meshLODNodes = new NativeList<AAAAMeshLODNode>(Allocator.Persistent);
            _meshletData = new NativeList<AAAAMeshlet>(Allocator.Persistent);
            _instanceData = new NativeList<AAAAInstanceData>(Allocator.Persistent);
            _materialData = new NativeList<AAAAMaterialData>(Allocator.Persistent);
            _sharedVertices = new NativeList<AAAAMeshletVertex>(Allocator.Persistent);
            _sharedIndices = new NativeList<byte>(Allocator.Persistent);

            AAAARendererAuthoringBase[] authorings = Object.FindObjectsByType<AAAARendererAuthoringBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            CreateInstances(authorings);
            if (_instanceData.Length == 0)
            {
                return;
            }

            AAAARenderPipelineRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAARenderPipelineRuntimeShaders>();
            _material = new Material(shaders.VisibilityBufferPS);

            _instanceDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                _instanceData.Length, UnsafeUtility.SizeOf<AAAAInstanceData>()
            )
            {
                name = "InstanceData",
            };
            _instanceDataBuffer.SetData(_instanceData.AsArray());

            _meshLODNodesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, math.max(1, _meshLODNodes.Length),
                UnsafeUtility.SizeOf<AAAAMeshLODNode>()
            )
            {
                name = "MeshLODNodes",
            };
            _meshLODNodesBuffer.SetData(_meshLODNodes.AsArray());

            _meshletsDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                _meshletData.Length, UnsafeUtility.SizeOf<AAAAMeshlet>()
            )
            {
                name = "MeshletsData",
            };
            _meshletsDataBuffer.SetData(_meshletData.AsArray());

            _materialDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                _materialData.Length, UnsafeUtility.SizeOf<AAAAMaterialData>()
            )
            {
                name = "MaterialData",
            };
            _materialDataBuffer.SetData(_materialData.AsArray());

            _sharedVertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                _sharedVertices.Length, UnsafeUtility.SizeOf<AAAAMeshletVertex>()
            );
            _sharedVertexBuffer.SetData(_sharedVertices.AsArray());

            int indicesToPad = _sharedIndices.Length % 4;
            for (int i = 0; i < indicesToPad; i++)
            {
                _sharedIndices.Add(0);
            }
            _sharedIndexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw,
                AAAAMathUtils.AlignUp(_sharedIndices.Length, sizeof(uint)) / sizeof(uint), sizeof(uint)
            );
            _sharedIndexBuffer.SetData(_sharedIndices.AsArray());

            MeshletRenderRequestsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw,
                AAAAMathUtils.AlignUp(4 * _meshletData.Length * UnsafeUtility.SizeOf<AAAAMeshletRenderRequestPacked>(), sizeof(uint)) / sizeof(uint),
                sizeof(uint)
            )
            {
                name = "VisibilityBuffer_MeshletRenderRequests",
            };

            IndirectDrawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawArgs.size)
            {
                name = "VisibilityBuffer_IndirectDrawArgs",
            };

            Texture2DArray albedoTextureArray = BuildTextureArray(_albedoTextures);
            Shader.SetGlobalTexture(ShaderIDs._SharedAlbedoTextureArray, albedoTextureArray);
        }

        public int MaxMeshletListBuildJobCount { get; private set; }

        public int MeshLODNodeCount => _meshLODNodes.Length;

        public int InstanceCount => _instanceData.IsCreated ? _instanceData.Length : 0;
        public GraphicsBuffer IndirectDrawArgsBuffer { get; }
        public GraphicsBuffer MeshletRenderRequestsBuffer { get; }

        public int MaxMeshLODLevelsCount { get; private set; }

        public void Dispose()
        {
            if (_meshLODNodes.IsCreated)
            {
                _meshLODNodes.Dispose();
            }

            if (_meshletData.IsCreated)
            {
                _meshletData.Dispose();
            }

            if (_instanceData.IsCreated)
            {
                _instanceData.Dispose();
            }

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
            _instanceDataBuffer?.Dispose();
            _materialDataBuffer?.Dispose();
            MeshletRenderRequestsBuffer?.Dispose();
        }

        public void PreRender(ScriptableRenderContext context)
        {
            CommandBuffer cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, Profiling.PreRender))
            {
                cmd.SetGlobalBuffer(ShaderIDs._Meshlets, _meshletsDataBuffer);
                cmd.SetGlobalBuffer(ShaderIDs._MeshLODNodes, _meshLODNodesBuffer);
                cmd.SetGlobalInt(ShaderIDs._ForcedMeshLODNodeDepth, GetForcedMeshLODNodeDepth());
                cmd.SetGlobalFloat(ShaderIDs._MeshLODErrorThreshold, GetMeshLODErrorThreshold());
                cmd.SetGlobalBuffer(ShaderIDs._SharedVertexBuffer, _sharedVertexBuffer);
                cmd.SetGlobalBuffer(ShaderIDs._SharedIndexBuffer, _sharedIndexBuffer);
                cmd.SetGlobalBuffer(ShaderIDs._InstanceData, _instanceDataBuffer);
                cmd.SetGlobalInt(ShaderIDs._InstanceCount, _instanceData.Length);
                cmd.SetGlobalBuffer(ShaderIDs._MaterialData, _materialDataBuffer);
                cmd.SetGlobalBuffer(ShaderIDs._MeshletRenderRequests, MeshletRenderRequestsBuffer);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Draw(IRasterCommandBuffer cmd)
        {
            if (IndirectDrawArgsBuffer != null && _instanceData.Length > 0)
            {
                cmd.DrawProceduralIndirect(Matrix4x4.identity, _material, 0, MeshTopology.Triangles, IndirectDrawArgsBuffer, 0);
            }
        }

        private int GetForcedMeshLODNodeDepth() => _debugDisplaySettings?.RenderingSettings.ForcedMeshLODNodeDepth ?? -1;

        private float GetMeshLODErrorThreshold() =>
            math.max(0, _meshLODSettings.ErrorThreshold + (_debugDisplaySettings?.RenderingSettings.MeshLODErrorThresholdBias ?? 0.0f));

        private void CreateInstances(AAAARendererAuthoringBase[] authorings)
        {
            foreach (AAAARendererAuthoringBase authoring in authorings)
            {
                AAAAMaterialAsset material = authoring.Material;
                AAAAMeshletCollectionAsset mesh = authoring.Mesh;
                if (material == null || mesh == null)
                {
                    continue;
                }

                Transform authoringTransform = authoring.transform;
                Matrix4x4 objectToWorldMatrix = authoringTransform.localToWorldMatrix;
                Matrix4x4 worldToObjectMatrix = authoringTransform.worldToLocalMatrix;

                var instanceData = new AAAAInstanceData
                {
                    ObjectToWorldMatrix = objectToWorldMatrix,
                    WorldToObjectMatrix = worldToObjectMatrix,
                    AABBMin = math.float4(mesh.Bounds.min, 0.0f),
                    AABBMax = math.float4(mesh.Bounds.max, 0.0f),
                    TopMeshLODStartIndex = (uint) GetOrAllocateMeshLODNodes(mesh),
                    TopMeshLODCount = (uint) mesh.MeshLODLevelNodeCounts[0],
                    TotalMeshLODCount = (uint) mesh.MeshLODNodes.Length,
                    MaterialIndex = (uint) GetOrAllocateMaterial(material),
                };
                _instanceData.Add(instanceData);

                MaxMeshletListBuildJobCount += Mathf.CeilToInt((float) instanceData.TotalMeshLODCount / AAAAMeshletListBuildJob.MaxLODNodesPerThreadGroup);
            }
        }

        private int GetOrAllocateMeshLODNodes(AAAAMeshletCollectionAsset meshletCollection)
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

        private int GetOrAllocateMaterial(AAAAMaterialAsset material)
        {
            if (_materialToIndex.TryGetValue(material, out int index))
            {
                return index;
            }

            var materialData = new AAAAMaterialData
            {
                AlbedoColor = (Vector4) material.AlbedoColor,
                AlbedoIndex = GetOrAllocateAlbedoTexture(material.Albedo),
            };
            _materialData.Add(materialData);
            index = _materialData.Length - 1;
            _materialToIndex.Add(material, index);
            return index;
        }

        private uint GetOrAllocateAlbedoTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return AAAAMaterialData.NoTextureIndex;
            }

            if (_textureToAlbedoIndex.TryGetValue(texture, out int index))
            {
                return (uint) index;
            }

            _albedoTextures.Add(texture);
            index = _albedoTextures.Count - 1;
            _textureToAlbedoIndex.Add(texture, index);
            return (uint) index;
        }

        private static Texture2DArray BuildTextureArray(List<Texture2D> textures)
        {
            if (textures.Count == 0)
            {
                return null;
            }

            Texture2D parametersSource = textures[0];
            var array = new Texture2DArray(parametersSource.width, parametersSource.height, textures.Count, parametersSource.format,
                parametersSource.mipmapCount, !parametersSource.isDataSRGB
            )
            {
                filterMode = parametersSource.filterMode,
                wrapMode = parametersSource.wrapMode,
            };

            for (int index = 0; index < array.depth; index++)
            {
                for (int mipIndex = 0; mipIndex < array.mipmapCount; mipIndex++)
                {
                    Graphics.CopyTexture(textures[index], 0, mipIndex, array, index, mipIndex);
                }
            }

            return array;
        }

        private static class Profiling
        {
            public static readonly ProfilingSampler PreRender = new("Visibility Buffer Container: Pre Render");
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
            public static readonly int _InstanceData = Shader.PropertyToID(nameof(_InstanceData));
            public static readonly int _InstanceCount = Shader.PropertyToID(nameof(_InstanceCount));
            public static readonly int _MaterialData = Shader.PropertyToID(nameof(_MaterialData));
            public static readonly int _MeshletRenderRequests = Shader.PropertyToID(nameof(_MeshletRenderRequests));

            public static readonly int _SharedAlbedoTextureArray = Shader.PropertyToID(nameof(_SharedAlbedoTextureArray));
        }
    }
}