using DELTation.AAAARP.Passes;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP
{
    public class AAAARenderer : AAAARendererBase
    {
        private readonly ClearCameraTargetPass _clearCameraTargetPass;
        private readonly ResolveCameraTargetPass _resolveCameraTargetPass;
        private readonly SkyboxPass _skyboxPass;
        
        public AAAARenderer()
        {
            _skyboxPass = new SkyboxPass(AAAARenderPassEvent.AfterRenderingOpaques);
            _resolveCameraTargetPass = new ResolveCameraTargetPass(AAAARenderPassEvent.AfterRendering);
            _clearCameraTargetPass = new ClearCameraTargetPass(AAAARenderPassEvent.BeforeRenderingGbuffer);
        }
        
        protected override void Setup(RenderGraph renderGraph, ScriptableRenderContext context)
        {
            EnqueuePass(_clearCameraTargetPass);
            EnqueuePass(_skyboxPass);
            EnqueuePass(_resolveCameraTargetPass);
        }
    }
}