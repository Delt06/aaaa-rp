using DELTation.AAAARP.Data;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Passes;
using DELTation.AAAARP.Passes.AntiAliasing;
using DELTation.AAAARP.Passes.IBL;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP
{
    public class AAAARenderer : AAAARendererBase
    {
        private readonly BilinearUpscalePass _bilinearUpscalePass;
        private readonly BRDFIntegrationPass _brdfIntegrationPass;
        private readonly ConvolveDiffuseIrradiancePass _convolveDiffuseIrradiancePass;
        private readonly DeferredLightingPass _deferredLightingPass;
        private readonly DrawVisibilityBufferPass _drawVisibilityBufferFalseNegativePass;
        private readonly DrawVisibilityBufferPass _drawVisibilityBufferMainPass;
        private readonly FinalBlitPass _finalBlitPass;
        private readonly GPUCullingPass _gpuCullingFalseNegativePass;
        private readonly GPUCullingPass _gpuCullingMainPass;
        private readonly HZBGenerationPass _hzbGenerationPass;
        private readonly AAAARenderPipelineAsset _pipelineAsset;
        private readonly PreFilterEnvironmentPass _preFilterEnvironmentPass;
        private readonly ResolveVisibilityBufferPass _resolveVisibilityBufferPass;
        private readonly SetupLightingPass _setupLightingPass;
        private readonly SkyboxPass _skyboxPass;
        private readonly SMAAPass _smaaPass;

        public AAAARenderer()
        {
            AAAARenderPipelineRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAARenderPipelineRuntimeShaders>();
            AAAARenderPipelineRuntimeTextures textures = GraphicsSettings.GetRenderPipelineSettings<AAAARenderPipelineRuntimeTextures>();

            _convolveDiffuseIrradiancePass = new ConvolveDiffuseIrradiancePass(AAAARenderPassEvent.BeforeRendering, shaders);
            _brdfIntegrationPass = new BRDFIntegrationPass(AAAARenderPassEvent.BeforeRendering, shaders);
            _preFilterEnvironmentPass = new PreFilterEnvironmentPass(AAAARenderPassEvent.BeforeRendering, shaders);
            _setupLightingPass = new SetupLightingPass(AAAARenderPassEvent.BeforeRendering);
            _gpuCullingMainPass = new GPUCullingPass(GPUCullingPass.PassType.Main, AAAARenderPassEvent.BeforeRenderingGbuffer, shaders,
                DebugHandler?.DisplaySettings
            );
            _gpuCullingFalseNegativePass = new GPUCullingPass(GPUCullingPass.PassType.FalseNegative, AAAARenderPassEvent.BeforeRenderingGbuffer, shaders,
                DebugHandler?.DisplaySettings
            );
            _hzbGenerationPass = new HZBGenerationPass(AAAARenderPassEvent.BeforeRenderingGbuffer, shaders);
            _drawVisibilityBufferMainPass =
                new DrawVisibilityBufferPass(DrawVisibilityBufferPass.PassType.Main, AAAARenderPassEvent.BeforeRenderingGbuffer);
            _drawVisibilityBufferFalseNegativePass =
                new DrawVisibilityBufferPass(DrawVisibilityBufferPass.PassType.FalseNegative, AAAARenderPassEvent.BeforeRenderingGbuffer);
            _resolveVisibilityBufferPass = new ResolveVisibilityBufferPass(AAAARenderPassEvent.BeforeRenderingGbuffer, shaders);
            _deferredLightingPass = new DeferredLightingPass(AAAARenderPassEvent.AfterRenderingGbuffer, shaders);
            _skyboxPass = new SkyboxPass(AAAARenderPassEvent.AfterRenderingOpaques);

            {
                const AAAARenderPassEvent antiAliasingEvent = AAAARenderPassEvent.BeforeRenderingPostProcessing;
                _bilinearUpscalePass = new BilinearUpscalePass(antiAliasingEvent);
                _smaaPass = new SMAAPass(antiAliasingEvent, shaders, textures);
            }

            _finalBlitPass = new FinalBlitPass(AAAARenderPassEvent.AfterRendering);
        }

        protected override void Setup(RenderGraph renderGraph, ScriptableRenderContext context, ContextContainer frameData)
        {
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();

            EnqueuePass(_convolveDiffuseIrradiancePass);
            EnqueuePass(_brdfIntegrationPass);
            EnqueuePass(_preFilterEnvironmentPass);

            EnqueuePass(_setupLightingPass);

            Camera cullingCameraOverride = DebugHandler?.GetGPUCullingCameraOverride();
            _gpuCullingMainPass.CullingCameraOverride = cullingCameraOverride;
            EnqueuePass(_gpuCullingMainPass);
            EnqueuePass(_drawVisibilityBufferMainPass);
            EnqueuePass(_hzbGenerationPass);
            EnqueuePass(_gpuCullingFalseNegativePass);
            EnqueuePass(_drawVisibilityBufferFalseNegativePass);
            EnqueuePass(_resolveVisibilityBufferPass);

            EnqueuePass(_deferredLightingPass);
            EnqueuePass(_skyboxPass);

            if (cameraData.AntiAliasingTechnique == AAAAAntiAliasingTechnique.SMAA)
            {
                EnqueuePass(_smaaPass);
            }
            EnqueuePass(_bilinearUpscalePass);
            EnqueuePass(_finalBlitPass);

            DebugHandler?.Setup(this, renderGraph, context);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            _convolveDiffuseIrradiancePass.Dispose();
            _brdfIntegrationPass.Dispose();
            _preFilterEnvironmentPass.Dispose();

            _gpuCullingMainPass.Dispose();
            _gpuCullingFalseNegativePass.Dispose();

            _smaaPass.Dispose();
        }
    }
}