using DELTation.AAAARP.Passes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP
{
    public class AAAARenderer : AAAARendererBase
    {
        private readonly DeferredLightingPass _deferredLightingPass;
        private readonly DrawVisibilityBufferPass _drawVisibilityBufferDepthOnlyPass;
        private readonly DrawVisibilityBufferPass _drawVisibilityBufferPass;
        private readonly FinalBlitPass _finalBlitPass;
        private readonly GPUCullingPass _gpuCullingFalseNegativePass;
        private readonly GPUCullingPass _gpuCullingMainPass;
        private readonly ResolveVisibilityBufferPass _resolveVisibilityBufferPass;
        private readonly SetupLightingPass _setupLightingPass;
        private readonly SkyboxPass _skyboxPass;

        public AAAARenderer()
        {
            AAAARenderPipelineRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAARenderPipelineRuntimeShaders>();
            AAAARenderPipelineDefaultTextures defaultTextures = GraphicsSettings.GetRenderPipelineSettings<AAAARenderPipelineDefaultTextures>();

            _setupLightingPass = new SetupLightingPass(AAAARenderPassEvent.BeforeRendering);
            _gpuCullingMainPass = new GPUCullingPass(GPUCullingPass.PassType.Main, AAAARenderPassEvent.BeforeRenderingGbuffer, shaders);
            _gpuCullingFalseNegativePass = new GPUCullingPass(GPUCullingPass.PassType.FalseNegative, AAAARenderPassEvent.BeforeRenderingGbuffer, shaders);
            _drawVisibilityBufferDepthOnlyPass =
                new DrawVisibilityBufferPass(DrawVisibilityBufferPass.PassType.Main, AAAARenderPassEvent.BeforeRenderingGbuffer);
            _drawVisibilityBufferPass =
                new DrawVisibilityBufferPass(DrawVisibilityBufferPass.PassType.FalseNegative, AAAARenderPassEvent.BeforeRenderingGbuffer);
            _resolveVisibilityBufferPass = new ResolveVisibilityBufferPass(AAAARenderPassEvent.BeforeRenderingGbuffer, shaders);
            _deferredLightingPass = new DeferredLightingPass(AAAARenderPassEvent.AfterRenderingGbuffer, shaders);
            _skyboxPass = new SkyboxPass(AAAARenderPassEvent.AfterRenderingOpaques);
            _finalBlitPass = new FinalBlitPass(AAAARenderPassEvent.AfterRendering);
        }

        protected override void Setup(RenderGraph renderGraph, ScriptableRenderContext context)
        {
            EnqueuePass(_setupLightingPass);

            Camera cullingCameraOverride = DebugHandler?.GetGPUCullingCameraOverride();
            _gpuCullingMainPass.CullingCameraOverride = cullingCameraOverride;
            EnqueuePass(_gpuCullingMainPass);
            EnqueuePass(_drawVisibilityBufferDepthOnlyPass);
            EnqueuePass(_gpuCullingFalseNegativePass);
            EnqueuePass(_drawVisibilityBufferPass);
            EnqueuePass(_resolveVisibilityBufferPass);

            EnqueuePass(_deferredLightingPass);
            EnqueuePass(_skyboxPass);

            EnqueuePass(_finalBlitPass);

            DebugHandler?.Setup(this, renderGraph, context);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _gpuCullingMainPass.Dispose();
            _gpuCullingFalseNegativePass.Dispose();
        }
    }
}