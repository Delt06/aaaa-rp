using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.Meshlets;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP
{
    public class AAAAVisibilityBufferContainer : MonoBehaviour
    {
        public AAAAMeshletCollection MeshletCollection;
        
        [Range(0, 100)]
        public int CommandCount = 2;
        
        private GraphicsBuffer _indirectArgsBuffer;
        private Material _material;
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
        }
        
        private void Update()
        {
            var indirectArgs = new NativeArray<GraphicsBuffer.IndirectDrawArgs>(CommandCount, Allocator.Temp);
            
            for (int i = 0; i < CommandCount; i++)
            {
                indirectArgs[i] = new GraphicsBuffer.IndirectDrawArgs
                {
                    startInstance = (uint) (i * MeshletCollection.Meshlets.Length),
                    instanceCount = (uint) MeshletCollection.Meshlets.Length,
                    startVertex = 0u,
                    vertexCountPerInstance = AAAAMeshletCollection.MeshletGenerationParams.MaxTriangles * 3,
                };
            }
            
            _indirectArgsBuffer.SetData(indirectArgs);
            
            _meshletsBuffer.SetData(MeshletCollection.Meshlets);
            _sharedVertexBuffer.SetData(MeshletCollection.VertexBuffer);
            _sharedIndexBuffer.SetData(MeshletCollection.IndexBuffer);
            
            var materialPropertyBlock = new MaterialPropertyBlock();
            materialPropertyBlock.SetInt(SharedIDs._MeshletCount, MeshletCollection.Meshlets.Length);
            materialPropertyBlock.SetBuffer(SharedIDs._Meshlets, _meshletsBuffer);
            materialPropertyBlock.SetBuffer(SharedIDs._SharedVertexBuffer, _sharedVertexBuffer);
            materialPropertyBlock.SetBuffer(SharedIDs._SharedIndexBuffer, _sharedIndexBuffer);
            var renderParams = new RenderParams(_material)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 100_000_000f),
                matProps = materialPropertyBlock,
            };
            Graphics.RenderPrimitivesIndirect(renderParams, MeshTopology.Triangles, _indirectArgsBuffer, CommandCount);
        }
        
        private void OnDestroy()
        {
            _indirectArgsBuffer?.Dispose();
            _meshletsBuffer?.Dispose();
            _sharedVertexBuffer?.Dispose();
            _sharedIndexBuffer?.Dispose();
        }
        
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class SharedIDs
        {
            public static readonly int _MeshletCount = Shader.PropertyToID(nameof(_MeshletCount));
            public static readonly int _Meshlets = Shader.PropertyToID(nameof(_Meshlets));
            public static readonly int _SharedVertexBuffer = Shader.PropertyToID(nameof(_SharedVertexBuffer));
            public static readonly int _SharedIndexBuffer = Shader.PropertyToID(nameof(_SharedIndexBuffer));
        }
    }
}