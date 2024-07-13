using DELTation.AAAARP.Passes;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP
{
    public class AAAARenderer : AAAARendererBase
    {
        private readonly DeferredLightingPass _deferredLightingPass;
        private readonly DrawVisibilityBufferPass _drawVisibilityBufferPass;
        private readonly FinalBlitPass _finalBlitPass;
        private readonly GPUMeshletCullingPass _gpuMeshletCullingPass;
        private readonly ResolveVisibilityBufferPass _resolveVisibilityBufferPass;
        private readonly SetupLightingPass _setupLightingPass;
        private readonly SkyboxPass _skyboxPass;

        public AAAARenderer()
        {
            AAAARenderPipelineRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAARenderPipelineRuntimeShaders>();
            AAAARenderPipelineDefaultTextures defaultTextures = GraphicsSettings.GetRenderPipelineSettings<AAAARenderPipelineDefaultTextures>();

            _setupLightingPass = new SetupLightingPass(AAAARenderPassEvent.BeforeRendering);
            _gpuMeshletCullingPass = new GPUMeshletCullingPass(AAAARenderPassEvent.BeforeRenderingGbuffer, shaders);
            _drawVisibilityBufferPass = new DrawVisibilityBufferPass(AAAARenderPassEvent.BeforeRenderingGbuffer);
            _resolveVisibilityBufferPass = new ResolveVisibilityBufferPass(AAAARenderPassEvent.BeforeRenderingGbuffer, shaders);
            _deferredLightingPass = new DeferredLightingPass(AAAARenderPassEvent.AfterRenderingGbuffer, shaders);
            _skyboxPass = new SkyboxPass(AAAARenderPassEvent.AfterRenderingOpaques);
            _finalBlitPass = new FinalBlitPass(AAAARenderPassEvent.AfterRendering);
        }

        protected override void Setup(RenderGraph renderGraph, ScriptableRenderContext context)
        {
            EnqueuePass(_setupLightingPass);

            _gpuMeshletCullingPass.CullingCameraOverride = DebugHandler?.GetGPUCullingCameraOverride();
            EnqueuePass(_gpuMeshletCullingPass);
            EnqueuePass(_drawVisibilityBufferPass);
            EnqueuePass(_resolveVisibilityBufferPass);

            EnqueuePass(_deferredLightingPass);
            EnqueuePass(_skyboxPass);

            EnqueuePass(_finalBlitPass);

            DebugHandler?.Setup(this, renderGraph, context);
        }
    }
}