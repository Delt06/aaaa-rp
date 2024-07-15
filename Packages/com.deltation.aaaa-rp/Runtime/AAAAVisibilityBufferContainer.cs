using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.Debugging;
using DELTation.AAAARP.Materials;
using DELTation.AAAARP.Meshlets;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
        private readonly Dictionary<AAAAMeshletCollectionAsset, int> _meshletCollectionToMeshLODIndex = new();
        private readonly GraphicsBuffer _meshletRenderRequestsBuffer;
        private readonly GraphicsBuffer _meshletsDataBuffer;
        private readonly GraphicsBuffer _meshLODBuffer;
        private readonly GraphicsBuffer _sharedIndexBuffer;
        private readonly GraphicsBuffer _sharedVertexBuffer;
        private readonly Dictionary<Texture2D, int> _textureToAlbedoIndex = new();
        private NativeList<AAAAInstanceData> _instanceData;
        private NativeList<AAAAMaterialData> _materialData;
        private NativeList<AAAAMeshlet> _meshletData;
        private NativeList<AAAAMeshletRenderRequest> _meshletRenderRequests;
        private NativeList<AAAAMeshLOD> _meshLODs;
        private NativeList<byte> _sharedIndices;
        private NativeList<AAAAMeshletVertex> _sharedVertices;

        internal AAAAVisibilityBufferContainer([CanBeNull] AAAARenderPipelineDebugDisplaySettings debugDisplaySettings)
        {
            _debugDisplaySettings = debugDisplaySettings;
            _meshletData = new NativeList<AAAAMeshlet>(Allocator.Persistent);
            _instanceData = new NativeList<AAAAInstanceData>(Allocator.Persistent);
            _materialData = new NativeList<AAAAMaterialData>(Allocator.Persistent);
            _meshletRenderRequests = new NativeList<AAAAMeshletRenderRequest>(Allocator.Persistent);
            _sharedVertices = new NativeList<AAAAMeshletVertex>(Allocator.Persistent);
            _sharedIndices = new NativeList<byte>(Allocator.Persistent);
            _meshLODs = new NativeList<AAAAMeshLOD>(Allocator.Persistent);

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
            );
            _instanceDataBuffer.SetData(_instanceData.AsArray());

            _meshletsDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                _meshletData.Length, UnsafeUtility.SizeOf<AAAAMeshlet>()
            );
            _meshletsDataBuffer.SetData(_meshletData.AsArray());

            _materialDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                _materialData.Length, UnsafeUtility.SizeOf<AAAAMaterialData>()
            );
            _materialDataBuffer.SetData(_materialData.AsArray());

            _sharedVertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                _sharedVertices.Length, UnsafeUtility.SizeOf<AAAAMeshletVertex>()
            );
            _sharedVertexBuffer.SetData(_sharedVertices.AsArray());

            _sharedIndexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw,
                AAAAMathUtils.AlignUp(_sharedIndices.Length, sizeof(uint)) / sizeof(uint), sizeof(uint)
            );
            _sharedIndexBuffer.SetData(_sharedIndices.AsArray());

            _meshLODBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                _meshLODs.Length, UnsafeUtility.SizeOf<AAAAMeshLOD>()
            );
            _meshLODBuffer.SetData(_meshLODs.AsArray());

            _meshletRenderRequestsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw,
                AAAAMathUtils.AlignUp(_meshletRenderRequests.Length * UnsafeUtility.SizeOf<AAAAMeshletRenderRequest>(), sizeof(uint)) / sizeof(uint),
                sizeof(uint)
            );
            _meshletRenderRequestsBuffer.SetData(_meshletRenderRequests.AsArray());

            IndirectArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments,
                1, GraphicsBuffer.IndirectDrawArgs.size
            );

            Texture2DArray albedoTextureArray = BuildTextureArray(_albedoTextures);
            Shader.SetGlobalTexture(ShaderIDs._SharedAlbedoTextureArray, albedoTextureArray);
        }

        public int InstanceCount => _instanceData.IsCreated ? _instanceData.Length : 0;
        public GraphicsBuffer IndirectArgsBuffer { get; }

        public void Dispose()
        {
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

            if (_meshletRenderRequests.IsCreated)
            {
                _meshletRenderRequests.Dispose();
            }

            if (_sharedVertices.IsCreated)
            {
                _sharedVertices.Dispose();
            }

            if (_sharedIndices.IsCreated)
            {
                _sharedIndices.Dispose();
            }

            if (_meshLODs.IsCreated)
            {
                _meshLODs.Dispose();
            }

            IndirectArgsBuffer?.Dispose();
            _meshletsDataBuffer?.Dispose();
            _sharedVertexBuffer?.Dispose();
            _sharedIndexBuffer?.Dispose();
            _instanceDataBuffer?.Dispose();
            _materialDataBuffer?.Dispose();
            _meshletRenderRequestsBuffer?.Dispose();
        }

        public void PreRender(ScriptableRenderContext context)
        {
            CommandBuffer cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, Profiling.PreRender))
            {
                cmd.SetGlobalBuffer(ShaderIDs._Meshlets, _meshletsDataBuffer);
                cmd.SetGlobalBuffer(ShaderIDs._MeshLODs, _meshLODBuffer);
                cmd.SetGlobalInt(ShaderIDs._MeshLODBias, _debugDisplaySettings?.RenderingSettings.MeshLODBias ?? 0);
                cmd.SetGlobalBuffer(ShaderIDs._SharedVertexBuffer, _sharedVertexBuffer);
                cmd.SetGlobalBuffer(ShaderIDs._SharedIndexBuffer, _sharedIndexBuffer);
                cmd.SetGlobalBuffer(ShaderIDs._InstanceData, _instanceDataBuffer);
                cmd.SetGlobalInt(ShaderIDs._InstanceCount, _instanceData.Length);
                cmd.SetGlobalBuffer(ShaderIDs._MaterialData, _materialDataBuffer);
                cmd.SetGlobalBuffer(ShaderIDs._MeshletRenderRequests, _meshletRenderRequestsBuffer);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            if (IndirectArgsBuffer != null && _instanceData.Length > 0)
            {
                var renderParams = new RenderParams(_material)
                {
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 100_000_000f),
                };
                Graphics.RenderPrimitivesIndirect(renderParams, MeshTopology.Triangles, IndirectArgsBuffer, 1);
            }
        }

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

                _instanceData.Add(new AAAAInstanceData
                    {
                        ObjectToWorldMatrix = objectToWorldMatrix,
                        WorldToObjectMatrix = worldToObjectMatrix,
                        MeshLODStartIndex = (uint) GetOrAllocateMeshLodIndex(mesh),
                        MaterialIndex = (uint) GetOrAllocateMaterial(material),
                    }
                );

                for (uint relativeMeshletID = 0; relativeMeshletID < mesh.Meshlets.Length; ++relativeMeshletID)
                {
                    _meshletRenderRequests.Add(default);
                }
            }
        }

        private int GetOrAllocateMeshLodIndex(AAAAMeshletCollectionAsset meshletCollection)
        {
            if (_meshletCollectionToMeshLODIndex.TryGetValue(meshletCollection, out int meshLodIndex))
            {
                return meshLodIndex;
            }

            uint triangleOffset = (uint) _sharedIndices.Length;
            uint vertexOffset = (uint) _sharedVertices.Length;
            uint meshletOffset = (uint) _meshletData.Length;
            meshLodIndex = _meshLODs.Length;

            foreach (AAAAMeshLOD sourceLOD in meshletCollection.Lods)
            {
                AAAAMeshLOD lod = sourceLOD;
                lod.MeshletStartOffset += meshletOffset;
                _meshLODs.Add(lod);
            }

            foreach (AAAAMeshlet sourceMeshlet in meshletCollection.Meshlets)
            {
                AAAAMeshlet meshlet = sourceMeshlet;
                meshlet.TriangleOffset += triangleOffset;
                meshlet.VertexOffset += vertexOffset;

                _meshletData.Add(meshlet);
            }

            AppendFromManagedArray(_sharedVertices, meshletCollection.VertexBuffer);
            AppendFromManagedArray(_sharedIndices, meshletCollection.IndexBuffer);

            _meshletCollectionToMeshLODIndex.Add(meshletCollection, meshLodIndex);
            return meshLodIndex;
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
            public static readonly int _MeshLODs = Shader.PropertyToID(nameof(_MeshLODs));
            public static readonly int _MeshLODBias = Shader.PropertyToID(nameof(_MeshLODBias));
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