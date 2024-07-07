using DELTation.AAAARP.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP
{
    public sealed partial class AAAARenderPipeline : RenderPipeline
    {
        public const string ShaderTagName = "AAAAPipeline";
        
        private readonly AAAARenderPipelineAsset _pipelineAsset;
        
        public AAAARenderPipeline(AAAARenderPipelineAsset pipelineAsset) => _pipelineAsset = pipelineAsset;
        
        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            Debug.Log($"Rendering {_pipelineAsset.name}");
        }
    }
}