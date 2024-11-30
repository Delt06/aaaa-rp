using DELTation.AAAARP.Data;
using DELTation.AAAARP.FrameData;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using static DELTation.AAAARP.AAAARenderPipelineCore;

namespace DELTation.AAAARP.Passes.Lighting
{
    public class SetupProbeVolumesPass : AAAARenderPass<SetupProbeVolumesPass.PassData>
    {
        private const bool VertexSamplingEnabled = false;

        public SetupProbeVolumesPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent) { }

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAARenderPipelineAsset pipelineAsset = renderingData.PipelineAsset;
            AAAALightingSettings.ProbeVolumesSettings probeVolumesSettings = pipelineAsset.LightingSettings.ProbeVolumes;

            passData.SupportsProbeVolumes = cameraData.SupportsProbeVolumes;
            passData.SupportsLightLayers = false;
            passData.ProbeVolumeSHBands = probeVolumesSettings.SHBands;
            passData.IsTemporalAAEnabled = false;
            passData.CameraType = cameraData.CameraType;
            passData.Camera = cameraData.Camera;
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            bool supportProbeVolume = data.SupportsProbeVolumes;
            ProbeReferenceVolume.instance.SetEnableStateFromSRP(supportProbeVolume);
            ProbeReferenceVolume.instance.SetVertexSamplingEnabled(VertexSamplingEnabled);

            // We need to verify and flush any pending asset loading for probe volume.
            if (supportProbeVolume && ProbeReferenceVolume.instance.isInitialized)
            {
                ProbeReferenceVolume.instance.PerformPendingOperations();
                if (data.CameraType != CameraType.Reflection &&
                    data.CameraType != CameraType.Preview)
                {
                    // TODO: Move this to one call for all cameras
                    ProbeReferenceVolume.instance.UpdateCellStreaming(context.cmd, data.Camera);
                }
            }

            if (supportProbeVolume)
            {
                ProbeReferenceVolume.instance.BindAPVRuntimeResources(context.cmd, true);
            }

            bool apvIsEnabled = data.SupportsProbeVolumes;

            context.cmd.SetKeyword(ShaderGlobalKeywords.PROBE_VOLUMES_L1, apvIsEnabled && data.ProbeVolumeSHBands == ProbeVolumeSHBands.SphericalHarmonicsL1);
            context.cmd.SetKeyword(ShaderGlobalKeywords.PROBE_VOLUMES_L2, apvIsEnabled && data.ProbeVolumeSHBands == ProbeVolumeSHBands.SphericalHarmonicsL2);

            VolumeStack stack = VolumeManager.instance.stack;
            bool enableProbeVolumes = ProbeReferenceVolume.instance.UpdateShaderVariablesProbeVolumes(
                context.cmd,
                stack.GetComponent<ProbeVolumesOptions>(),
                data.IsTemporalAAEnabled ? Time.frameCount : 0,
                data.SupportsLightLayers
            );

            context.cmd.SetGlobalInt(ShaderPropertyID._EnableProbeVolumes, enableProbeVolumes ? 1 : 0);
        }

        public class PassData : PassDataBase
        {
            public Camera Camera;
            public CameraType CameraType;
            public bool IsTemporalAAEnabled;
            public ProbeVolumeSHBands ProbeVolumeSHBands;
            public bool SupportsLightLayers;
            public bool SupportsProbeVolumes;
        }
    }
}