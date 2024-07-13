using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.Materials;
using DELTation.AAAARP.Meshlets;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace DELTation.AAAARP
{
    public class AAAAVisibilityBufferContainer : MonoBehaviour
    {
        public AAAAMeshletCollection MeshletCollection;
        public AAAAMaterialAsset[] Materials;
        
        public Vector3 Scale = Vector3.one;
        public float MaxDistance = 10.0f;
        
        [Range(0, 100)]
        public int CommandCount = 2;
        
        [Min(0)]
        public int InstanceCount = 1;
        
        public int Seed;
        
        private GraphicsBuffer _indirectArgsBuffer;
        private GraphicsBuffer _instanceDataBuffer;
        private Material _material;
        private GraphicsBuffer _materialDataBuffer;
        private GraphicsBuffer _meshletsBuffer;
        private GraphicsBuffer _sharedIndexBuffer;
        private GraphicsBuffer _sharedVertexBuffer;
        
        private void Awake()
        {
            AAAARenderPipelineRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAARenderPipelineRuntimeShaders>();
            _material = new Material(shaders.VisibilityBufferPS);
            
            _indirectArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments,
                100, GraphicsBuffer.IndirectDrawArgs.size
            );
            _meshletsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                MeshletCollection.Meshlets.Length, UnsafeUtility.SizeOf<AAAAMeshlet>()
            );
            _sharedVertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                MeshletCollection.VertexBuffer.Length, UnsafeUtility.SizeOf<AAAAMeshletVertex>()
            );
            _sharedIndexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw,
                AAAAMathUtils.AlignUp(MeshletCollection.IndexBuffer.Length / sizeof(uint), sizeof(uint)), sizeof(uint)
            );
            _instanceDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                100,
                UnsafeUtility.SizeOf<AAAAInstanceData>()
            );
            
            CreateMaterials();
        }
        
        private void Update()
        {
            Random.State oldRandomState = Random.state;
            Random.InitState(Seed);
            
            var indirectArgs = new NativeArray<GraphicsBuffer.IndirectDrawArgs>(CommandCount, Allocator.Temp);
            
            for (int i = 0; i < CommandCount; i++)
            {
                indirectArgs[i] = new GraphicsBuffer.IndirectDrawArgs
                {
                    startInstance = (uint) (i * InstanceCount),
                    instanceCount = (uint) InstanceCount,
                    startVertex = 0u,
                    vertexCountPerInstance = (uint) MeshletCollection.Meshlets.Length * AAAAMeshletConfiguration.MaxMeshletIndices,
                };
            }
            
            _indirectArgsBuffer.SetData(indirectArgs);
            
            _meshletsBuffer.SetData(MeshletCollection.Meshlets);
            _sharedVertexBuffer.SetData(MeshletCollection.VertexBuffer);
            _sharedIndexBuffer.SetData(MeshletCollection.IndexBuffer);
            
            var perInstanceData = new NativeArray<AAAAInstanceData>(InstanceCount * CommandCount, Allocator.Temp);
            for (int i = 0; i < perInstanceData.Length; i++)
            {
                var objectToWorld = Matrix4x4.TRS(
                    Random.insideUnitSphere * MaxDistance,
                    Random.rotationUniform,
                    Scale
                );
                perInstanceData[i] = new AAAAInstanceData
                {
                    ObjectToWorldMatrix = objectToWorld,
                    WorldToObjectMatrix = objectToWorld.inverse,
                    MaterialIndex = (uint) Random.Range(0, _materialDataBuffer.count),
                };
            }
            
            _instanceDataBuffer.SetData(perInstanceData);
            
            Shader.SetGlobalInt(ShaderIDs._MeshletCount, MeshletCollection.Meshlets.Length);
            Shader.SetGlobalBuffer(ShaderIDs._Meshlets, _meshletsBuffer);
            Shader.SetGlobalBuffer(ShaderIDs._SharedVertexBuffer, _sharedVertexBuffer);
            Shader.SetGlobalBuffer(ShaderIDs._SharedIndexBuffer, _sharedIndexBuffer);
            Shader.SetGlobalBuffer(ShaderIDs._InstanceData, _instanceDataBuffer);
            var renderParams = new RenderParams(_material)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 100_000_000f),
            };
            Graphics.RenderPrimitivesIndirect(renderParams, MeshTopology.Triangles, _indirectArgsBuffer, CommandCount);
            
            Random.state = oldRandomState;
        }
        
        private void OnDestroy()
        {
            _indirectArgsBuffer?.Dispose();
            _meshletsBuffer?.Dispose();
            _sharedVertexBuffer?.Dispose();
            _sharedIndexBuffer?.Dispose();
            _instanceDataBuffer?.Dispose();
            _materialDataBuffer?.Dispose();
        }
        
        private void CreateMaterials()
        {
            var materialData = new NativeArray<AAAAMaterialData>(Materials.Length, Allocator.Temp, NativeArrayOptions.ClearMemory);
            using ObjectPool<List<Texture2D>>.PooledObject _ = ListPool<Texture2D>.Get(out List<Texture2D> albedoTextures);
            
            for (int index = 0; index < Materials.Length; index++)
            {
                ref AAAAMaterialData materialDataValue = ref materialData.ElementAtRef(index);
                materialDataValue = default;
                
                AAAAMaterialAsset materialAsset = Materials[index];
                int albedoIndex = albedoTextures.IndexOf(materialAsset.Albedo);
                if (albedoIndex == -1)
                {
                    albedoTextures.Add(materialAsset.Albedo);
                    materialDataValue.AlbedoIndex = (uint) (albedoTextures.Count - 1);
                }
                else
                {
                    materialDataValue.AlbedoIndex = (uint) albedoIndex;
                }
            }
            
            _materialDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, materialData.Length, UnsafeUtility.SizeOf<AAAAMaterialData>());
            _materialDataBuffer.SetData(materialData);
            Shader.SetGlobalBuffer(ShaderIDs._MaterialData, _materialDataBuffer);
            
            Texture2DArray albedoTextureArray = BuildTextureArray(albedoTextures);
            Shader.SetGlobalTexture(ShaderIDs._SharedAlbedoTextureArray, albedoTextureArray);
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
        
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static readonly int _MeshletCount = Shader.PropertyToID(nameof(_MeshletCount));
            public static readonly int _Meshlets = Shader.PropertyToID(nameof(_Meshlets));
            public static readonly int _SharedVertexBuffer = Shader.PropertyToID(nameof(_SharedVertexBuffer));
            public static readonly int _SharedIndexBuffer = Shader.PropertyToID(nameof(_SharedIndexBuffer));
            public static readonly int _InstanceData = Shader.PropertyToID(nameof(_InstanceData));
            public static readonly int _MaterialData = Shader.PropertyToID(nameof(_MaterialData));
            public static readonly int _SharedAlbedoTextureArray = Shader.PropertyToID(nameof(_SharedAlbedoTextureArray));
        }
    }
}