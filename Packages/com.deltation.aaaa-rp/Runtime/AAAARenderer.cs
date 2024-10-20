using System;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.Data;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Passes;
using DELTation.AAAARP.Passes.AntiAliasing;
using DELTation.AAAARP.Passes.ClusteredLighting;
using DELTation.AAAARP.Passes.GlobalIllumination.AO;
using DELTation.AAAARP.Passes.GlobalIllumination.SSR;
using DELTation.AAAARP.Passes.IBL;
using DELTation.AAAARP.Passes.Lighting;
using DELTation.AAAARP.Passes.PostProcessing;
using DELTation.AAAARP.Passes.Shadows;
using DELTation.AAAARP.RenderPipelineResources;
using DELTation.AAAARP.Volumes;
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
        private readonly ColorHistoryPass _colorHistoryPass;
        private readonly ConvolveDiffuseIrradiancePass _convolveDiffuseIrradiancePass;
        private readonly DeferredLightingPass _deferredLightingPass;
        private readonly DeferredReflectionsComposePass _deferredReflectionsComposePass;
        private readonly Material _deferredReflectionsMaterial;
        private readonly DeferredReflectionsSetupPass _deferredReflectionsSetupPass;
        private readonly DrawVisibilityBufferPass _drawVisibilityBufferFalseNegativePass;
        private readonly DrawVisibilityBufferPass _drawVisibilityBufferMainPass;
        private readonly FinalBlitPass _finalBlitPass;
        private readonly FSRPass _fsrPass;
        private readonly GPUCullingPass _gpuCullingFalseNegativePass;
        private readonly HZBGenerationPass _gpuCullingHzbGenerationPass;
        private readonly GPUCullingPass _gpuCullingMainPass;
        private readonly PreFilterEnvironmentPass _preFilterEnvironmentPass;
        private readonly ResolveVisibilityBufferPass _resolveVisibilityBufferPass;
        private readonly SetupLightingPass _setupLightingPass;
        private readonly ShadowPassPool _shadowPassPool;
        private readonly SkyboxPass _skyboxPass;
        private readonly SMAAPass _smaaPass;
        private readonly SSRComposePass _ssrComposePass;
        private readonly HZBGenerationPass _ssrHzbGenerationPass;
        private readonly Material _ssrResolveMaterial;
        private readonly SSRResolvePass _ssrResolvePass;
        private readonly SSRTracePass _ssrTracePass;
        private readonly UberPostProcessingPass _uberPostProcessingPass;
        private readonly XeGTAOPass _xeGTAOPass;

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
            _gpuCullingHzbGenerationPass =
                new HZBGenerationPass(AAAARenderPassEvent.BeforeRenderingGbuffer, HZBGenerationPass.Mode.Max, "GPUCulling.", shaders);
            _drawVisibilityBufferMainPass =
                new DrawVisibilityBufferPass(DrawVisibilityBufferPass.PassType.Main, AAAARenderPassEvent.BeforeRenderingGbuffer);
            _drawVisibilityBufferFalseNegativePass =
                new DrawVisibilityBufferPass(DrawVisibilityBufferPass.PassType.FalseNegative, AAAARenderPassEvent.BeforeRenderingGbuffer);
            _resolveVisibilityBufferPass = new ResolveVisibilityBufferPass(AAAARenderPassEvent.BeforeRenderingGbuffer, shaders);

            _deferredReflectionsMaterial = CoreUtils.CreateEngineMaterial(shaders.DeferredReflectionsPS);
            const AAAARenderPassEvent deferredReflectionsRenderPassEvent = AAAARenderPassEvent.AfterRenderingGbuffer;
            _deferredReflectionsSetupPass = new DeferredReflectionsSetupPass(deferredReflectionsRenderPassEvent, _deferredReflectionsMaterial);
            _deferredReflectionsComposePass = new DeferredReflectionsComposePass(deferredReflectionsRenderPassEvent, _deferredReflectionsMaterial);
            _ssrResolveMaterial = CoreUtils.CreateEngineMaterial(shaders.SsrResolvePS);
            _ssrHzbGenerationPass = new HZBGenerationPass(deferredReflectionsRenderPassEvent, HZBGenerationPass.Mode.Min, "SSR.", shaders);
            _ssrTracePass = new SSRTracePass(deferredReflectionsRenderPassEvent, shaders);
            _ssrComposePass = new SSRComposePass(deferredReflectionsRenderPassEvent, _ssrResolveMaterial);
            _ssrResolvePass = new SSRResolvePass(deferredReflectionsRenderPassEvent, _ssrResolveMaterial);

            _clusteredLightingPass = new ClusteredLightingPass(AAAARenderPassEvent.AfterRenderingGbuffer, shaders);
            _deferredLightingPass = new DeferredLightingPass(AAAARenderPassEvent.AfterRenderingGbuffer, shaders);
            _xeGTAOPass = new XeGTAOPass(AAAARenderPassEvent.AfterRenderingGbuffer);
            _skyboxPass = new SkyboxPass(AAAARenderPassEvent.AfterRenderingOpaques);
            _colorHistoryPass = new ColorHistoryPass(AAAARenderPassEvent.AfterRenderingTransparents);

            _smaaPass = new SMAAPass(AAAARenderPassEvent.BeforeRenderingPostProcessing, shaders, textures);
            _uberPostProcessingPass = new UberPostProcessingPass(AAAARenderPassEvent.BeforeRenderingPostProcessing, shaders);

            const AAAARenderPassEvent upscaleRenderPassEvent = AAAARenderPassEvent.AfterRenderingPostProcessing;
            _bilinearUpscalePass = new BilinearUpscalePass(upscaleRenderPassEvent);
            _fsrPass = new FSRPass(upscaleRenderPassEvent);

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
            _gpuCullingFalseNegativePass.CullingCameraOverride = cullingCameraOverride;
            EnqueuePass(_gpuCullingMainPass);
            EnqueuePass(_drawVisibilityBufferMainPass);
            EnqueuePass(_gpuCullingHzbGenerationPass);
            EnqueuePass(_gpuCullingFalseNegativePass);
            EnqueuePass(_drawVisibilityBufferFalseNegativePass);
            EnqueuePass(_resolveVisibilityBufferPass);

            EnqueuePass(_clusteredLightingPass);
            if (cameraData.AmbientOcclusionTechnique == AAAAAmbientOcclusionTechnique.XeGTAO)
            {
                EnqueuePass(_xeGTAOPass);
            }
            EnqueuePass(_deferredLightingPass);
            EnqueuePass(_skyboxPass);

            {
                AAAASsrVolumeComponent ssr = cameraData.VolumeStack.GetComponent<AAAASsrVolumeComponent>();

                if (ssr.Enabled.value)
                {
                    EnqueuePass(_ssrHzbGenerationPass);
                    EnqueuePass(_ssrTracePass);
                    EnqueuePass(_ssrResolvePass);
                }

                EnqueuePass(_deferredReflectionsSetupPass);

                if (ssr.Enabled.value)
                {
                    EnqueuePass(_ssrComposePass);
                }

                EnqueuePass(_deferredReflectionsComposePass);
            }

            EnqueuePass(_colorHistoryPass);

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

            CoreUtils.Destroy(_ssrResolveMaterial);
            CoreUtils.Destroy(_deferredReflectionsMaterial);
        }
    }
}