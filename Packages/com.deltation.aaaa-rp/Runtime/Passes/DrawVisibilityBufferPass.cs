using System;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Renderers;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes
{
    public sealed class DrawVisibilityBufferPass : AAAARasterRenderPass<DrawVisibilityBufferPass.PassData>
    {
        public enum PassType
        {
            Main,
            FalseNegative,
        }

        private readonly PassType _passType;

        public DrawVisibilityBufferPass(PassType passType, AAAARenderPassEvent renderPassEvent) : base(renderPassEvent)
        {
            _passType = passType;
            Name = AutoName + "." + passType;
        }

        public override string Name { get; }

        protected override void Setup(IRasterRenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            AAAARendererListData rendererListData = frameData.Get<AAAARendererListData>();
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();

            passData.CameraType = cameraData.CameraType;

            passData.RendererListHandle = _passType switch
            {
                PassType.Main => rendererListData.VisibilityBufferMain.Handle,
                PassType.FalseNegative => rendererListData.VisibilityBufferFalseNegative.Handle,
                var _ => throw new ArgumentOutOfRangeException(),
            };
            passData.RendererContainer = renderingData.RendererContainer;

            builder.UseRendererList(passData.RendererListHandle);
            builder.SetRenderAttachment(resourceData.VisibilityBuffer, 0, AccessFlags.ReadWrite);
            builder.SetRenderAttachmentDepth(resourceData.CameraScaledDepthBuffer, AccessFlags.ReadWrite);

            builder.SetGlobalTextureAfterPass(resourceData.VisibilityBuffer, AAAAResourceData.ShaderPropertyID._VisibilityBuffer);
            builder.SetGlobalTextureAfterPass(resourceData.CameraScaledDepthBuffer, AAAAResourceData.ShaderPropertyID._CameraDepth);
        }

        protected override void Render(PassData data, RasterGraphContext context)
        {
            data.RendererContainer.Draw(data.CameraType, context.cmd, AAAARendererContainer.PassType.Visibility);
            context.cmd.DrawRendererList(data.RendererListHandle);
        }

        public class PassData : PassDataBase
        {
            public CameraType CameraType;
            public AAAARendererContainer RendererContainer;
            public RendererListHandle RendererListHandle;
        }
    }
}