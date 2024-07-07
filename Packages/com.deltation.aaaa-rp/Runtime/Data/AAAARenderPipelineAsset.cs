using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Data
{
    [CreateAssetMenu(menuName = "AAAA RP/AAAA Render Pipeline Asset")]
    public sealed class AAAARenderPipelineAsset : RenderPipelineAsset<AAAARenderPipeline>, IRenderGraphEnabledRenderPipeline
    {
        public override string renderPipelineShaderTag => AAAARenderPipeline.ShaderTagName;
        
        public bool isImmediateModeSupported => false;
        
        protected override RenderPipeline CreatePipeline() => new AAAARenderPipeline(this);
    }
}