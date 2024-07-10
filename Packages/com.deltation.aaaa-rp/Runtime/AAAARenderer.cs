using DELTation.AAAARP.Passes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP
{
    public class AAAARenderer : AAAARendererBase
    {
        private readonly DeferredLightingPass _deferredLightingPass;
        private readonly DrawVisibilityBufferPass _drawVisibilityBufferPass;
        private readonly FinalBlitPass _finalBlitPass;
        private readonly ResolveVisibilityBufferPass _resolveVisibilityBufferPass;
        private readonly SkyboxPass _skyboxPass;
        
        private Material _visibilityBufferResolveMaterial;
        
        public AAAARenderer()
        {
            AAAARenderPipelineRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAARenderPipelineRuntimeShaders>();
            AAAARenderPipelineDefaultTextures defaultTextures = GraphicsSettings.GetRenderPipelineSettings<AAAARenderPipelineDefaultTextures>();
            _visibilityBufferResolveMaterial = CoreUtils.CreateEngineMaterial(shaders.VisibilityBufferResolvePS);
            _visibilityBufferResolveMaterial.SetTexture("_Albedo", defaultTextures.UVTest);
            
            _drawVisibilityBufferPass = new DrawVisibilityBufferPass(AAAARenderPassEvent.BeforeRenderingGbuffer);
            _resolveVisibilityBufferPass = new ResolveVisibilityBufferPass(AAAARenderPassEvent.BeforeRenderingGbuffer, _visibilityBufferResolveMaterial);
            _deferredLightingPass = new DeferredLightingPass(AAAARenderPassEvent.AfterRenderingGbuffer);
            _skyboxPass = new SkyboxPass(AAAARenderPassEvent.AfterRenderingOpaques);
            _finalBlitPass = new FinalBlitPass(AAAARenderPassEvent.AfterRendering);
        }
        
        protected override void Setup(RenderGraph renderGraph, ScriptableRenderContext context)
        {
            EnqueuePass(_drawVisibilityBufferPass);
            EnqueuePass(_resolveVisibilityBufferPass);
            
            EnqueuePass(_deferredLightingPass);
            EnqueuePass(_skyboxPass);
            
            EnqueuePass(_finalBlitPass);
        }
        
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            
            CoreUtils.Destroy(_visibilityBufferResolveMaterial);
            _visibilityBufferResolveMaterial = null;
        }
    }
}