using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Data;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Utils;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.GlobalIllumination.SSR
{
    public class SSRResolvePass : AAAARenderPass<SSRResolvePass.PassData>
    {
        private const int ResolvePass = 0;
        private const int BilateralBlurPass = 1;

        private readonly Material _material;

        public SSRResolvePass(AAAARenderPassEvent renderPassEvent, Material material) : base(renderPassEvent) => _material = material;

        public override string Name => "SSR.Resolve";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAALightingData lightingData = frameData.Get<AAAALightingData>();
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();

            passData.TraceResult = builder.ReadTexture(lightingData.SSRTraceResult);

            int2 traceResultSize = lightingData.SSRTraceResultSize;
            const string resolveResultName = "SSR" + nameof(PassData.ResolveResult);
            TextureDesc resolveResultDesc = AAAARenderingUtils.CreateTextureDesc(resolveResultName,
                new RenderTextureDescriptor(
                    traceResultSize.x, traceResultSize.y, GraphicsFormat.R8G8B8A8_SRGB, GraphicsFormat.None
                )
            );
            lightingData.SSRResolveResult = renderingData.RenderGraph.CreateTexture(resolveResultDesc);
            passData.ResolveResult = builder.WriteTexture(lightingData.SSRResolveResult);

            resolveResultDesc.name = resolveResultName + "_Pong";
            passData.ResolveResultPong = builder.CreateTransientTexture(resolveResultDesc);
            passData.ResolveResultTexelSize = 1.0f / (float2) traceResultSize;

            passData.CameraColor = builder.ReadTexture(resourceData.CameraScaledColorBuffer);

            AAAALightingSettings.SSRSettings ssrSettings = renderingData.PipelineAsset.LightingSettings.SSR;
            passData.BlurSmooth = ssrSettings.BlurSmooth;
            passData.BlurRough = ssrSettings.BlurRough;

            builder.AllowPassCulling(false);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            using (new ProfilingScope(context.cmd, Profiling.ResolveUV))
            {
                ResolveUV(context.cmd, data);
            }

            using (new ProfilingScope(context.cmd, Profiling.BilateralBlur))
            {
                BilateralBlur(context.cmd, data, data.ResolveResult, data.ResolveResultPong, math.float2(1, 0));
                BilateralBlur(context.cmd, data, data.ResolveResultPong, data.ResolveResult, math.float2(0, 1));
            }
        }

        private void ResolveUV(CommandBuffer cmd, PassData data)
        {
            MaterialPropertyBlock propertyBlock = data.PropertyBlock;

            cmd.SetRenderTarget(data.ResolveResult);

            propertyBlock.Clear();
            propertyBlock.SetVector(ShaderID._BlitScaleBias, new Vector4(1, 1, 0, 0));
            propertyBlock.SetTexture(ShaderID._SSRTraceResult, data.TraceResult);
            propertyBlock.SetTexture(ShaderID._CameraColor, data.CameraColor);

            AAAABlitter.BlitTriangle(cmd, _material, ResolvePass, propertyBlock);
        }

        private void BilateralBlur(CommandBuffer cmd, PassData data, TextureHandle source, TextureHandle destination, float2 direction)
        {
            MaterialPropertyBlock propertyBlock = data.PropertyBlock;

            cmd.SetRenderTarget(destination);

            propertyBlock.Clear();
            propertyBlock.SetVector(ShaderID._BlitScaleBias, new Vector4(1, 1, 0, 0));

            float2 blurVector = data.ResolveResultTexelSize * direction;
            propertyBlock.SetVector(ShaderID._BlurVectorRange, new Vector4(blurVector.x, blurVector.y, data.BlurSmooth, data.BlurRough));
            propertyBlock.SetTexture(ShaderID._SSRResolveResult, source);
            propertyBlock.SetTexture(ShaderID._SSRTraceResult, data.TraceResult);

            AAAABlitter.BlitTriangle(cmd, _material, BilateralBlurPass, propertyBlock);
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderID
        {
            public static readonly int _BlitScaleBias = Shader.PropertyToID(nameof(_BlitScaleBias));
            public static readonly int _BlurVectorRange = Shader.PropertyToID(nameof(_BlurVectorRange));
            public static readonly int _SSRTraceResult = Shader.PropertyToID(nameof(_SSRTraceResult));
            public static readonly int _SSRResolveResult = Shader.PropertyToID(nameof(_SSRResolveResult));
            public static readonly int _CameraColor = Shader.PropertyToID(nameof(_CameraColor));
        }

        private static class Profiling
        {
            public static readonly ProfilingSampler ResolveUV = new(nameof(ResolveUV));
            public static readonly ProfilingSampler BilateralBlur = new(nameof(BilateralBlur));
        }

        public class PassData : PassDataBase
        {
            public readonly MaterialPropertyBlock PropertyBlock = new();
            public float BlurRough;
            public float BlurSmooth;
            public TextureHandle CameraColor;
            public TextureHandle ResolveResult;
            public TextureHandle ResolveResultPong;
            public float2 ResolveResultTexelSize;
            public TextureHandle TraceResult;
        }
    }
}