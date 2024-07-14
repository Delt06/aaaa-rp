using System;
using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Debugging;
using DELTation.AAAARP.FrameData;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.Debugging
{
    public class VisibilityBufferDebugPassPassData : PassDataBase { }

    public class VisibilityBufferDebugPass : AAAARasterRenderPass<VisibilityBufferDebugPassPassData>, IDisposable
    {
        private readonly AAAARenderPipelineDebugDisplaySettings _debugDisplaySettings;
        private readonly Material _material;

        public VisibilityBufferDebugPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineDebugShaders shaders,
            AAAARenderPipelineDebugDisplaySettings debugDisplaySettings) : base(renderPassEvent)
        {
            _material = CoreUtils.CreateEngineMaterial(shaders.VisibilityBufferDebugPS);
            _debugDisplaySettings = debugDisplaySettings;
        }

        public override string Name => "VisibilityBufferDebug";

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
        }

        protected override void Setup(IRasterRenderGraphBuilder builder, VisibilityBufferDebugPassPassData passData, ContextContainer frameData)
        {
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();

            builder.UseTexture(resourceData.VisibilityBuffer, AccessFlags.Read);
            builder.SetRenderAttachment(resourceData.CameraColorBuffer, 0, AccessFlags.ReadWrite);
            builder.SetRenderAttachmentDepth(resourceData.CameraDepthBuffer, AccessFlags.Read);
            builder.AllowGlobalStateModification(true);
        }

        protected override void Render(VisibilityBufferDebugPassPassData data, RasterGraphContext context)
        {
            AAAAVisibilityBufferDebugMode visibilityBufferDebugMode = _debugDisplaySettings.RenderingSettings.GetOverridenVisibilityBufferDebugMode();
            context.cmd.SetGlobalInt(ShaderID._VisibilityBufferDebugMode, (int) visibilityBufferDebugMode);

            var scaleBias = new Vector4(1, 1, 0, 0);
            const int pass = 0;
            Blitter.BlitTexture(context.cmd, scaleBias, _material, pass);
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderID
        {
            public static int _VisibilityBufferDebugMode = Shader.PropertyToID(nameof(_VisibilityBufferDebugMode));
        }
    }
}