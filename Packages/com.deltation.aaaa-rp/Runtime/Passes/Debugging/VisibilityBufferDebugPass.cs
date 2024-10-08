using System;
using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Debugging;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.Debugging
{
    public sealed class VisibilityBufferDebugPass : AAAARasterRenderPass<VisibilityBufferDebugPass.PassData>, IDisposable
    {
        private readonly AAAARenderPipelineDebugDisplaySettings _debugDisplaySettings;
        private readonly Material _material;

        public VisibilityBufferDebugPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineDebugShaders shaders,
            AAAARenderPipelineDebugDisplaySettings debugDisplaySettings) : base(renderPassEvent)
        {
            _material = CoreUtils.CreateEngineMaterial(shaders.VisibilityBufferDebugPS);
            _debugDisplaySettings = debugDisplaySettings;
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
        }

        protected override void Setup(IRasterRenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();

            builder.UseTexture(resourceData.VisibilityBuffer, AccessFlags.Read);
            builder.SetRenderAttachment(resourceData.CameraScaledColorBuffer, 0, AccessFlags.ReadWrite);
            builder.SetRenderAttachmentDepth(resourceData.CameraScaledDepthBuffer, AccessFlags.Read);
            builder.AllowGlobalStateModification(true);
        }

        protected override void Render(PassData data, RasterGraphContext context)
        {
            AAAAVisibilityBufferDebugMode debugMode = _debugDisplaySettings.RenderingSettings.GetOverridenVisibilityBufferDebugMode();
            context.cmd.SetGlobalInt(ShaderID._VisibilityBufferDebugMode, (int) debugMode);

            var scaleBias = new Vector4(1, 1, 0, 0);
            const int pass = 0;
            Blitter.BlitTexture(context.cmd, scaleBias, _material, pass);
        }

        public class PassData : PassDataBase { }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderID
        {
            public static int _VisibilityBufferDebugMode = Shader.PropertyToID(nameof(_VisibilityBufferDebugMode));
        }
    }
}