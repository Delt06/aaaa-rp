using System;
using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using DELTation.AAAARP.Utils;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
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
            passData.ResolveResult = builder.CreateTransientTexture(AAAARenderingUtils.CreateTextureDesc("SSRResolveResult", new RenderTextureDescriptor(
                        cameraData.ScaledWidth, cameraData.ScaledHeight, GraphicsFormat.R8G8B8A8_SRGB, GraphicsFormat.None
                    )
                )
            );
            passData.CameraColor = builder.ReadTexture(resourceData.CameraScaledColorBuffer);
            passData.CameraDepth = builder.ReadTexture(resourceData.CameraScaledDepthBuffer);

            builder.AllowPassCulling(false);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            using (new ProfilingScope(context.cmd, Profiling.ResolveUV))
            {
                context.cmd.SetRenderTarget(data.ResolveResult);

                _propertyBlock.Clear();
                _propertyBlock.SetVector(ShaderID._BlitScaleBias, new Vector4(1, 1, 0, 0));
                _propertyBlock.SetTexture(ShaderID._SSRTraceResult, data.TraceResult);
                _propertyBlock.SetTexture(ShaderID._CameraColor, data.CameraColor);

                const int shaderPass = 0;
                AAAABlitter.BlitTriangle(context.cmd, _resolveMaterial, shaderPass, _propertyBlock);
            }

            using (new ProfilingScope(context.cmd, Profiling.Compose))
            {
                context.cmd.SetRenderTarget(data.CameraColor, data.CameraDepth);

                _propertyBlock.Clear();
                _propertyBlock.SetVector(ShaderID._BlitScaleBias, new Vector4(1, 1, 0, 0));
                _propertyBlock.SetTexture(ShaderID._SSRResolveResult, data.ResolveResult);

                const int shaderPass = 1;
                AAAABlitter.BlitTriangle(context.cmd, _resolveMaterial, shaderPass, _propertyBlock);
            }
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderID
        {
            public static readonly int _BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
            public static readonly int _SSRTraceResult = Shader.PropertyToID("_SSRTraceResult");
            public static readonly int _SSRResolveResult = Shader.PropertyToID("_SSRResolveResult");
            public static readonly int _CameraColor = Shader.PropertyToID("_CameraColor");
        }

        private static class Profiling
        {
            public static readonly ProfilingSampler ResolveUV = new(nameof(ResolveUV));
            public static readonly ProfilingSampler Compose = new(nameof(Compose));
        }

        public class PassData : PassDataBase
        {
            public TextureHandle CameraColor;
            public TextureHandle ResolveResult;
            public TextureHandle TraceResult;
            public TextureHandle CameraDepth;
        }
    }
}