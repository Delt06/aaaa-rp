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
    public sealed class GPUCullingDebugViewPass : AAAARasterRenderPass<GPUCullingDebugViewPass.PassData>, IDisposable
    {
        private readonly AAAARenderPipelineDebugDisplaySettings _displaySettings;
        private readonly Material _material;

        public GPUCullingDebugViewPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineDebugShaders debugShaders,
            AAAARenderPipelineDebugDisplaySettings displaySettings) : base(renderPassEvent)
        {
            _material = CoreUtils.CreateEngineMaterial(debugShaders.GPUCullingDebugViewPS);
            _material.name = Name;
            _displaySettings = displaySettings;
        }

        public override string Name => "GPUCulling.Debug.View";

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
        }

        protected override void Setup(IRasterRenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAADebugData debugData = frameData.Get<AAAADebugData>();
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();

            passData.Buffer = builder.UseBuffer(debugData.GPUCullingDebugBuffer, AccessFlags.Read);

            builder.SetRenderAttachment(resourceData.CameraScaledColorBuffer, 0, AccessFlags.ReadWrite);

            builder.AllowGlobalStateModification(true);

            _material.SetFloat(ShaderIDs._InstanceCountLimit, _displaySettings.RenderingSettings.DebugGPUCullingViewInstanceCountLimit);
            _material.SetFloat(ShaderIDs._MeshletCountLimit, _displaySettings.RenderingSettings.DebugGPUCullingViewMeshletCountLimit);
            _material.SetFloat(ShaderIDs._Mode, (float) _displaySettings.RenderingSettings.GPUCullingDebugViewMode);
        }

        protected override void Render(PassData data, RasterGraphContext context)
        {
            context.cmd.SetGlobalBuffer(ShaderIDs._GPUCullingDebugDataBuffer, data.Buffer);

            var scaleBias = new Vector4(1, 1, 0, 0);
            const int pass = 0;
            Blitter.BlitTexture(context.cmd, scaleBias, _material, pass);
        }

        public class PassData : PassDataBase
        {
            public BufferHandle Buffer;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static readonly int _InstanceCountLimit = Shader.PropertyToID(nameof(_InstanceCountLimit));
            public static readonly int _MeshletCountLimit = Shader.PropertyToID(nameof(_MeshletCountLimit));
            public static readonly int _Mode = Shader.PropertyToID(nameof(_Mode));

            public static readonly int _GPUCullingDebugDataBuffer = Shader.PropertyToID(nameof(_GPUCullingDebugDataBuffer));
        }
    }
}