using System;
using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Debugging;
using DELTation.AAAARP.FrameData;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.Debugging
{
    public sealed class GBufferDebugPass : AAAARasterRenderPass<GBufferDebugPass.PassData>, IDisposable
    {
        private readonly AAAARenderPipelineDebugDisplaySettings _debugDisplaySettings;
        private readonly Material _material;

        public GBufferDebugPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineDebugShaders shaders,
            AAAARenderPipelineDebugDisplaySettings debugDisplaySettings) : base(renderPassEvent)
        {
            _material = CoreUtils.CreateEngineMaterial(shaders.GBufferDebugPS);
            _debugDisplaySettings = debugDisplaySettings;
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
        }

        protected override void Setup(IRasterRenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            passData.NearPlane = cameraData.Camera.nearClipPlane;
            passData.FarPlane = cameraData.Camera.farClipPlane;

            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();

            builder.UseTexture(resourceData.GBufferAlbedo, AccessFlags.Read);
            builder.UseTexture(resourceData.GBufferNormals, AccessFlags.Read);
            builder.UseTexture(resourceData.GBufferMasks, AccessFlags.Read);
            builder.UseTexture(resourceData.CameraDepthBuffer, AccessFlags.Read);
            builder.SetRenderAttachment(resourceData.CameraColorBuffer, 0, AccessFlags.ReadWrite);
            builder.AllowGlobalStateModification(true);
        }

        protected override void Render(PassData data, RasterGraphContext context)
        {
            int debugMode = (int) _debugDisplaySettings.RenderingSettings.GBufferDebugMode;
            context.cmd.SetGlobalInt(ShaderID._GBufferDebugMode, debugMode);

            Vector2 remap = _debugDisplaySettings.RenderingSettings.GBufferDebugDepthRemap;
            context.cmd.SetGlobalVector(ShaderID._GBufferDebugDepthRemap, new Vector4(
                    math.unlerp(data.NearPlane, data.FarPlane, remap.x),
                    math.unlerp(data.NearPlane, data.FarPlane, remap.y)
                )
            );

            var scaleBias = new Vector4(1, 1, 0, 0);
            const int pass = 0;
            Blitter.BlitTexture(context.cmd, scaleBias, _material, pass);
        }

        public class PassData : PassDataBase
        {
            public float FarPlane;
            public float NearPlane;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderID
        {
            public static int _GBufferDebugMode = Shader.PropertyToID(nameof(_GBufferDebugMode));
            public static int _GBufferDebugDepthRemap = Shader.PropertyToID(nameof(_GBufferDebugDepthRemap));
        }
    }
}