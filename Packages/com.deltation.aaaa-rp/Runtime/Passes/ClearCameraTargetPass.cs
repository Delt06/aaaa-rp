using DELTation.AAAARP.FrameData;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes
{
    public class ClearCameraTargetPassData : PassDataBase
    {
        public Color BackgroundColor;
        public bool ClearColor;
        public bool ClearDepth;
    }
    
    public class ClearCameraTargetPass : AAAARasterRenderPass<ClearCameraTargetPassData>
    {
        public ClearCameraTargetPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent) { }
        
        public override string Name => "ClearCameraTarget";
        
        protected override void Setup(IRasterRenderGraphBuilder builder, ClearCameraTargetPassData passData, ContextContainer frameData)
        {
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            
            switch (cameraData.Camera.clearFlags)
            {
                case CameraClearFlags.Skybox:
                    passData.ClearDepth = true;
                    passData.ClearColor = false;
                    break;
                case CameraClearFlags.Color:
                    passData.ClearDepth = true;
                    passData.ClearColor = true;
                    passData.BackgroundColor = cameraData.Camera.backgroundColor;
                    break;
                default:
                    passData.ClearColor = false;
                    passData.ClearDepth = false;
                    break;
            }
            
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            builder.SetRenderAttachment(resourceData.CameraColorBuffer, 0, AccessFlags.WriteAll);
            builder.SetRenderAttachmentDepth(resourceData.CameraDepthBuffer, AccessFlags.WriteAll);
        }
        
        protected override void Render(ClearCameraTargetPassData data, RasterGraphContext context)
        {
            context.cmd.ClearRenderTarget(data.ClearDepth, data.ClearColor, data.BackgroundColor);
        }
    }
}