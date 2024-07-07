using DELTation.AAAARP.FrameData;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes
{
    public class BlitToCameraTargetPassData : PassDataBase
    {
        public Color ClearColor;
    }
    
    public class BlitToCameraTargetPass : AAAARasterRenderPass<BlitToCameraTargetPassData>
    {
        public BlitToCameraTargetPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent) { }
        
        public override string Name => "BlitToCameraTarget";
        
        protected override void Setup(IRasterRenderGraphBuilder builder, BlitToCameraTargetPassData passData, ContextContainer frameData)
        {
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            passData.ClearColor = cameraData.Camera.backgroundColor;
            
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            builder.SetRenderAttachment(resourceData.CameraResolveColorBuffer, 0, AccessFlags.Write);
        }
        
        protected override void Render(BlitToCameraTargetPassData data, RasterGraphContext context)
        {
            const bool clearColor = true;
            const bool clearDepth = false;
            context.cmd.ClearRenderTarget(clearDepth, clearColor, data.ClearColor);
        }
    }
}