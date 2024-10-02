﻿using System;
using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Debugging;
using DELTation.AAAARP.FrameData;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.Debugging
{
    public sealed class LightingDebugPass : AAAARasterRenderPass<LightingDebugPass.PassData>, IDisposable
    {
        private readonly AAAARenderPipelineDebugDisplaySettings _debugDisplaySettings;
        private readonly Material _material;

        public LightingDebugPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineDebugShaders shaders,
            AAAARenderPipelineDebugDisplaySettings debugDisplaySettings) : base(renderPassEvent)
        {
            _material = CoreUtils.CreateEngineMaterial(shaders.LightingDebugPS);
            _debugDisplaySettings = debugDisplaySettings;
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
        }

        protected override void Setup(IRasterRenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();

            builder.UseTexture(resourceData.CameraScaledDepthBuffer, AccessFlags.Read);
            builder.SetRenderAttachment(resourceData.CameraScaledColorBuffer, 0, AccessFlags.ReadWrite);
            builder.AllowGlobalStateModification(true);
        }

        protected override void Render(PassData data, RasterGraphContext context)
        {
            int debugMode = (int) _debugDisplaySettings.RenderingSettings.LightingDebugMode;
            context.cmd.SetGlobalInt(ShaderID._LightingDebugMode, debugMode);

            Vector2 remap = _debugDisplaySettings.RenderingSettings.LightingDebugCountRemap;
            context.cmd.SetGlobalVector(ShaderID._LightingDebugCountRemap, remap);

            var scaleBias = new Vector4(1, 1, 0, 0);
            const int pass = 0;
            Blitter.BlitTexture(context.cmd, scaleBias, _material, pass);
        }

        public class PassData : PassDataBase { }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderID
        {
            public static int _LightingDebugMode = Shader.PropertyToID(nameof(_LightingDebugMode));
            public static int _LightingDebugCountRemap = Shader.PropertyToID(nameof(_LightingDebugCountRemap));
        }
    }
}