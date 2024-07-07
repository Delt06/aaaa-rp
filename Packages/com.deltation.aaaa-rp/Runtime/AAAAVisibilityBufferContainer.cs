using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP
{
    public class AAAAVisibilityBufferContainer : MonoBehaviour
    {
        [Min(0)]
        public int VertexCount = 6;
        [Min(0)]
        public int InstanceCount = 5;
        [Range(0, 100)]
        public int CommandCount = 2;
        
        private GraphicsBuffer _indirectArgsBuffer;
        private Material _material;
        
        private void Awake()
        {
            AAAARenderPipelineRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAARenderPipelineRuntimeShaders>();
            _material = new Material(shaders.VisibilityBufferPS);
            
            _indirectArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 100, GraphicsBuffer.IndirectDrawArgs.size);
        }
        
        private void Update()
        {
            var indirectArgs = new NativeArray<GraphicsBuffer.IndirectDrawArgs>(CommandCount, Allocator.Temp);
            
            for (int i = 0; i < CommandCount; i++)
            {
                indirectArgs[i] = new GraphicsBuffer.IndirectDrawArgs
                {
                    startInstance = (uint)(i * InstanceCount),
                    instanceCount = (uint) InstanceCount,
                    startVertex = 0u,
                    vertexCountPerInstance = (uint) VertexCount,
                };
            }
            
            _indirectArgsBuffer.SetData(indirectArgs);
            
            var renderParams = new RenderParams(_material)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 100_000_000f),
            };
            Graphics.RenderPrimitivesIndirect(renderParams, MeshTopology.Triangles, _indirectArgsBuffer, CommandCount);
        }
        
        private void OnDestroy()
        {
            if (_indirectArgsBuffer != null)
            {
                _indirectArgsBuffer.Dispose();
                _indirectArgsBuffer = null;
            }
        }
    }
}