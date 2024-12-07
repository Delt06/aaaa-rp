using System;
using DELTation.AAAARP.Data;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.Lighting
{
    public sealed class DeferredLightingPass : AAAARasterRenderPass<DeferredLightingPass.PassData>, IDisposable
    {
        private readonly Material _material;

        public DeferredLightingPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders shaders) : base(renderPassEvent) =>
            _material = CoreUtils.CreateEngineMaterial(shaders.DeferredLightingPS);

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
        }

        protected override void Setup(IRasterRenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAALightingData lightingData = frameData.Get<AAAALightingData>();

            // Always run the direct lighting pass - for unlit materials.
            passData.ApplyDirect = true;
            passData.ApplyIndirect = lightingData.AmbientIntensity > 0;

            passData.ScaleBias = new Vector4(1, 1, 0, 0);

            builder.UseTexture(resourceData.GBufferAlbedo, AccessFlags.Read);
            builder.UseTexture(resourceData.GBufferNormals, AccessFlags.Read);
            builder.UseTexture(resourceData.GBufferMasks, AccessFlags.Read);
            builder.UseTexture(resourceData.CameraScaledDepthBuffer, AccessFlags.Read);

            if (cameraData.AmbientOcclusionTechnique == AAAAAmbientOcclusionTechnique.XeGTAO)
            {
                builder.UseTexture(lightingData.GTAOTerm, AccessFlags.Read);
            }

            if (cameraData.RealtimeGITechnique == AAAARealtimeGITechnique.LightPropagationVolumes)
            {
                builder.UseTexture(lightingData.LPVGridRedSH, AccessFlags.Read);
                passData.ApplyIndirect = true;
            }

            builder.SetRenderAttachment(resourceData.CameraScaledColorBuffer, 0, AccessFlags.WriteAll);
        }

        protected override void Render(PassData data, RasterGraphContext context)
        {
            if (data.ApplyDirect)
            {
                using (new ProfilingScope(context.cmd, Profiling.Direct))
                {
                    const int directPass = 0;
                    Blitter.BlitTexture(context.cmd, data.ScaleBias, _material, directPass);
                }
            }

            if (data.ApplyIndirect)
            {
                using (new ProfilingScope(context.cmd, Profiling.Indirect))
                {
                    const int indirectPass = 1;
                    Blitter.BlitTexture(context.cmd, data.ScaleBias, _material, indirectPass);
                }
            }
        }

        public class PassData : PassDataBase
        {
            public bool ApplyDirect;
            public bool ApplyIndirect;
            public Vector4 ScaleBias;
        }

        private static class Profiling
        {
            public static readonly ProfilingSampler Direct = new(nameof(Direct));
            public static readonly ProfilingSampler Indirect = new(nameof(Indirect));
        }
    }
}