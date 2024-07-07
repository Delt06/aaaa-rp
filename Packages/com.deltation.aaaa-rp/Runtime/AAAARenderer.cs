using DELTation.AAAARP.Passes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP
{
    public class AAAARenderer : AAAARendererBase
    {
        private readonly DrawVisibilityBufferPass _drawVisibilityBufferPass;
        private readonly ResolveVisibilityBufferPass _resolveVisibilityBufferPass;
        private readonly FinalBlitPass _finalBlitPass;
        private readonly SkyboxPass _skyboxPass;
        
        private Material _visibilityBufferPreviewMaterial;
        
        public AAAARenderer()
        {
            AAAARenderPipelineRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAARenderPipelineRuntimeShaders>();
            _visibilityBufferPreviewMaterial = CoreUtils.CreateEngineMaterial(shaders.VisibilityBufferPreviewPS);
            
            _drawVisibilityBufferPass = new DrawVisibilityBufferPass(AAAARenderPassEvent.BeforeRenderingGbuffer);
            _resolveVisibilityBufferPass = new ResolveVisibilityBufferPass(AAAARenderPassEvent.BeforeRenderingGbuffer, _visibilityBufferPreviewMaterial);
            _skyboxPass = new SkyboxPass(AAAARenderPassEvent.AfterRenderingOpaques);
            _finalBlitPass = new FinalBlitPass(AAAARenderPassEvent.AfterRendering);
        }
        
        protected override void Setup(RenderGraph renderGraph, ScriptableRenderContext context)
        {
            EnqueuePass(_drawVisibilityBufferPass);
            EnqueuePass(_resolveVisibilityBufferPass);
            EnqueuePass(_skyboxPass);
            EnqueuePass(_finalBlitPass);
        }
        
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            
            CoreUtils.Destroy(_visibilityBufferPreviewMaterial);
            _visibilityBufferPreviewMaterial = null;
        }
    }
}