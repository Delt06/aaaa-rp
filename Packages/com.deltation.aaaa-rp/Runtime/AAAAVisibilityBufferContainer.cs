using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
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
        
        public Vector3 Scale = Vector3.one;
        public float MaxDistance = 10.0f;
        
        [Range(0, 100)]
        public int CommandCount = 2;
        
        [Min(0)]
        public int InstanceCount = 1;
        
        public int Seed;
        
        private GraphicsBuffer _indirectArgsBuffer;
        private Material _material;
        private GraphicsBuffer _meshletsBuffer;
        private GraphicsBuffer _perInstanceDataBuffer;
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
            _perInstanceDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                100,
                UnsafeUtility.SizeOf<AAAAPerInstanceData>()
            );
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
                    vertexCountPerInstance = (uint) MeshletCollection.Meshlets.Length * AAAAMeshletCollection.MaxMeshletIndices,
                };
            }
            
            _indirectArgsBuffer.SetData(indirectArgs);
            
            _meshletsBuffer.SetData(MeshletCollection.Meshlets);
            _sharedVertexBuffer.SetData(MeshletCollection.VertexBuffer);
            _sharedIndexBuffer.SetData(MeshletCollection.IndexBuffer);
            
            var perInstanceData = new NativeArray<AAAAPerInstanceData>(InstanceCount * CommandCount, Allocator.Temp);
            for (int i = 0; i < perInstanceData.Length; i++)
            {
                var objectToWorld = Matrix4x4.TRS(
                    Random.insideUnitSphere * MaxDistance,
                    Random.rotationUniform,
                    Scale
                );
                perInstanceData[i] = new AAAAPerInstanceData
                {
                    ObjectToWorldMatrix = objectToWorld,
                    WorldToObjectMatrix = objectToWorld.inverse,
                };
            }
            
            _perInstanceDataBuffer.SetData(perInstanceData);
            
            Shader.SetGlobalInt(SharedIDs._MeshletCount, MeshletCollection.Meshlets.Length);
            Shader.SetGlobalBuffer(SharedIDs._Meshlets, _meshletsBuffer);
            Shader.SetGlobalBuffer(SharedIDs._SharedVertexBuffer, _sharedVertexBuffer);
            Shader.SetGlobalBuffer(SharedIDs._SharedIndexBuffer, _sharedIndexBuffer);
            Shader.SetGlobalBuffer(SharedIDs._PerInstanceData, _perInstanceDataBuffer);
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
            _perInstanceDataBuffer?.Dispose();
        }
        
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class SharedIDs
        {
            public static readonly int _MeshletCount = Shader.PropertyToID(nameof(_MeshletCount));
            public static readonly int _Meshlets = Shader.PropertyToID(nameof(_Meshlets));
            public static readonly int _SharedVertexBuffer = Shader.PropertyToID(nameof(_SharedVertexBuffer));
            public static readonly int _SharedIndexBuffer = Shader.PropertyToID(nameof(_SharedIndexBuffer));
            public static readonly int _PerInstanceData = Shader.PropertyToID(nameof(_PerInstanceData));
        }
    }
}