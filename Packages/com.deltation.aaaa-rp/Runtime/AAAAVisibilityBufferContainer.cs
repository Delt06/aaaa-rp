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
        
        private Material _material;
        
        private void Awake()
        {
            AAAARenderPipelineRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAARenderPipelineRuntimeShaders>();
            _material = new Material(shaders.VisibilityBufferPS);
        }
        
        private void Update()
        {
            var renderParams = new RenderParams(_material)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 100_000_000f),
            };
            Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, VertexCount, InstanceCount);
        }
    }
}