using System;
using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using DELTation.AAAARP.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.GlobalIllumination.SSR
{
    public class SSRResolvePass : AAAARenderPass<SSRResolvePass.PassData>, IDisposable
    {
        private readonly MaterialPropertyBlock _propertyBlock = new();
        private readonly Material _resolveMaterial;

        public SSRResolvePass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders shaders) : base(renderPassEvent) =>
            _resolveMaterial = CoreUtils.CreateEngineMaterial(shaders.SsrResolvePS);

        public override string Name => "SSR.Resolve";

        public void Dispose()
        {
            CoreUtils.Destroy(_resolveMaterial);
        }

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAALightingData lightingData = frameData.Get<AAAALightingData>();
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();

            passData.TraceResult = builder.ReadTexture(lightingData.SSRTraceResult);
            passData.Result = builder.CreateTransientTexture(AAAARenderingUtils.CreateTextureDesc("SSRResolveResult", new RenderTextureDescriptor(
                        cameraData.ScaledWidth, cameraData.ScaledHeight, cameraData.CameraTargetDescriptor.colorFormat
                    )
                )
            );
            passData.CameraColor = builder.ReadTexture(resourceData.CameraScaledColorBuffer);

            builder.AllowPassCulling(false);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            context.cmd.SetRenderTarget(data.Result);

            _propertyBlock.SetVector(ShaderID._BlitScaleBias, new Vector4(1, 1, 0, 0));
            _propertyBlock.SetTexture(ShaderID._SSRTraceResult, data.TraceResult);
            _propertyBlock.SetTexture(ShaderID._CameraColor, data.CameraColor);

            const int shaderPass = 0;
            AAAABlitter.BlitTriangle(context.cmd, _resolveMaterial, shaderPass, _propertyBlock);
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderID
        {
            public static readonly int _BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
            public static readonly int _SSRTraceResult = Shader.PropertyToID("_SSRTraceResult");
            public static readonly int _CameraColor = Shader.PropertyToID("_CameraColor");
        }

        public class PassData : PassDataBase
        {
            public TextureHandle CameraColor;
            public TextureHandle Result;
            public TextureHandle TraceResult;
        }
    }
}