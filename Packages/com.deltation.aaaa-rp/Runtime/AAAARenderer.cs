using System;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.Data;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Passes;
using DELTation.AAAARP.Passes.AntiAliasing;
using DELTation.AAAARP.Passes.ClusteredLighting;
using DELTation.AAAARP.Passes.GlobalIllumination;
using DELTation.AAAARP.Passes.IBL;
using DELTation.AAAARP.Passes.PostProcessing;
using DELTation.AAAARP.Passes.Shadows;
using DELTation.AAAARP.RenderPipelineResources;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP
{
    public class AAAARenderer : AAAARendererBase
    {
        private readonly BilinearUpscalePass _bilinearUpscalePass;
        private readonly BRDFIntegrationPass _brdfIntegrationPass;
        private readonly ClusteredLightingPass _clusteredLightingPass;
        private readonly ConvolveDiffuseIrradiancePass _convolveDiffuseIrradiancePass;
        private readonly DeferredLightingPass _deferredLightingPass;
        private readonly DrawVisibilityBufferPass _drawVisibilityBufferFalseNegativePass;
        private readonly DrawVisibilityBufferPass _drawVisibilityBufferMainPass;
        private readonly FinalBlitPass _finalBlitPass;
        private readonly FsrPass _fsrPass;
        private readonly GPUCullingPass _gpuCullingFalseNegativePass;
        private readonly GPUCullingPass _gpuCullingMainPass;
        private readonly HZBGenerationPass _hzbGenerationPass;
        private readonly AAAARenderPipelineAsset _pipelineAsset;
        private readonly PreFilterEnvironmentPass _preFilterEnvironmentPass;
        private readonly ResolveVisibilityBufferPass _resolveVisibilityBufferPass;
        private readonly SetupLightingPass _setupLightingPass;
        private readonly ShadowPassPool _shadowPassPool;
        private readonly SkyboxPass _skyboxPass;
        private readonly SmaaPass _smaaPass;
        private readonly UberPostProcessingPass _uberPostProcessingPass;
        private readonly XeGtaoPass _xeGTAOPass;

        public AAAARenderer()
        {
            AAAARenderPipelineRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAARenderPipelineRuntimeShaders>();
            AAAARenderPipelineRuntimeTextures textures = GraphicsSettings.GetRenderPipelineSettings<AAAARenderPipelineRuntimeTextures>();

            _convolveDiffuseIrradiancePass = new ConvolveDiffuseIrradiancePass(AAAARenderPassEvent.BeforeRendering, shaders);
            _brdfIntegrationPass = new BRDFIntegrationPass(AAAARenderPassEvent.BeforeRendering, shaders);
            _preFilterEnvironmentPass = new PreFilterEnvironmentPass(AAAARenderPassEvent.BeforeRendering, shaders);

            _shadowPassPool = new ShadowPassPool(AAAARenderPassEvent.BeforeRenderingShadows, shaders, DebugHandler?.DisplaySettings);
            _setupLightingPass = new SetupLightingPass(AAAARenderPassEvent.AfterRenderingShadows);

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

            _clusteredLightingPass = new ClusteredLightingPass(AAAARenderPassEvent.AfterRenderingGbuffer, shaders);
            _deferredLightingPass = new DeferredLightingPass(AAAARenderPassEvent.AfterRenderingGbuffer, shaders);
            _xeGTAOPass = new XeGtaoPass(AAAARenderPassEvent.AfterRenderingGbuffer);
            _skyboxPass = new SkyboxPass(AAAARenderPassEvent.AfterRenderingOpaques);

            _smaaPass = new SmaaPass(AAAARenderPassEvent.BeforeRenderingPostProcessing, shaders, textures);
            _uberPostProcessingPass = new UberPostProcessingPass(AAAARenderPassEvent.BeforeRenderingPostProcessing, shaders);

            const AAAARenderPassEvent upscaleRenderPassEvent = AAAARenderPassEvent.AfterRenderingPostProcessing;
            _bilinearUpscalePass = new BilinearUpscalePass(upscaleRenderPassEvent);
            _fsrPass = new FsrPass(upscaleRenderPassEvent);

            _finalBlitPass = new FinalBlitPass(AAAARenderPassEvent.AfterRendering);
        }

        protected override void Setup(RenderGraph renderGraph, ScriptableRenderContext context, ContextContainer frameData)
        {
            _shadowPassPool.Reset();

            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAAShadowsData shadowsData = frameData.Get<AAAAShadowsData>();

            EnqueuePass(_convolveDiffuseIrradiancePass);
            EnqueuePass(_brdfIntegrationPass);
            EnqueuePass(_preFilterEnvironmentPass);

            for (int shadowLightIndex = 0; shadowLightIndex < shadowsData.ShadowLights.Length; shadowLightIndex++)
            {
                ref readonly AAAAShadowsData.ShadowLight shadowLight = ref shadowsData.ShadowLights.ElementAtRef(shadowLightIndex);
                for (int splitIndex = 0; splitIndex < shadowLight.Splits.Length; splitIndex++)
                {
                    ref readonly AAAAShadowsData.ShadowLightSplit shadowLightSplit = ref shadowLight.Splits.ElementAtRef(splitIndex);
                    ShadowPassPool.PassSet passSet = _shadowPassPool.RequestPassesBasic(shadowLightIndex, splitIndex, shadowLightSplit.CullingView);
                    EnqueuePass(passSet.GPUCullingPass);
                    EnqueuePass(passSet.DrawShadowsPass);
                }
            }
            EnqueuePass(_setupLightingPass);

            Camera cullingCameraOverride = DebugHandler?.GetGPUCullingCameraOverride();
            _gpuCullingMainPass.CullingCameraOverride = cullingCameraOverride;
            EnqueuePass(_gpuCullingMainPass);
            EnqueuePass(_drawVisibilityBufferMainPass);
            EnqueuePass(_hzbGenerationPass);
            EnqueuePass(_gpuCullingFalseNegativePass);
            EnqueuePass(_drawVisibilityBufferFalseNegativePass);
            EnqueuePass(_resolveVisibilityBufferPass);

            EnqueuePass(_clusteredLightingPass);
            EnqueuePass(_xeGTAOPass);
            EnqueuePass(_deferredLightingPass);
            EnqueuePass(_skyboxPass);

            if (cameraData.AntiAliasingTechnique == AAAAAntiAliasingTechnique.SMAA)
            {
                EnqueuePass(_smaaPass);
            }

            if (cameraData.PostProcessingEnabled)
            {
                EnqueuePass(_uberPostProcessingPass);
            }

            switch (cameraData.UpscalingTechnique)
            {
                case AAAAUpscalingTechnique.Off:
                    EnqueuePass(_bilinearUpscalePass);
                    break;
                case AAAAUpscalingTechnique.FSR1:
                    EnqueuePass(_fsrPass);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            EnqueuePass(_finalBlitPass);

            DebugHandler?.Setup(this, renderGraph, context);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            _shadowPassPool.Dispose();

            _convolveDiffuseIrradiancePass.Dispose();
            _brdfIntegrationPass.Dispose();
            _preFilterEnvironmentPass.Dispose();

            _gpuCullingMainPass.Dispose();
            _gpuCullingFalseNegativePass.Dispose();

            _uberPostProcessingPass.Dispose();
            _smaaPass.Dispose();
        }
    }
}