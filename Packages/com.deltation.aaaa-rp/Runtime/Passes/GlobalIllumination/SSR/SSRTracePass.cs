using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.GlobalIllumination.SSR
{
    public class SSRTracePass : AAAARenderPass<SSRTracePass.PassData>
    {
        private readonly ComputeShader _traceCS;

        public SSRTracePass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders shaders) : base(renderPassEvent) =>
            _traceCS = shaders.SsrTraceCS;

        public override string Name => "SSR.Trace";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            AAAALightingData lightingData = frameData.Get<AAAALightingData>();

            lightingData.SSRTraceResult = renderingData.RenderGraph.CreateTexture(
                AAAARenderingUtils.CreateTextureDesc("SSRTraceResult",
                    new RenderTextureDescriptor(cameraData.ScaledWidth, cameraData.ScaledHeight, GraphicsFormat.R16G16_UNorm, GraphicsFormat.None)
                    {
                        enableRandomWrite = true,
                    }
                )
            );

            passData.ScreenSizePixels = new Vector4(
                cameraData.ScaledWidth, cameraData.ScaledHeight,
                1.0f / cameraData.ScaledWidth, 1.0f / cameraData.ScaledHeight
            );
            passData.Result = builder.WriteTexture(lightingData.SSRTraceResult);

            Matrix4x4 viewProjMatrix = cameraData.GetGPUProjectionMatrixJittered(true) * cameraData.ViewMatrix;
            passData.ViewProjMatrix = viewProjMatrix;
            passData.InvViewProjMatrix = viewProjMatrix.inverse;
            passData.CameraPosition = cameraData.Camera.transform.position;

            builder.ReadTexture(resourceData.CameraScaledDepthBuffer);
            builder.ReadTexture(resourceData.GBufferNormals);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            const int kernelIndex = 0;
            const int threadGroupSize = SSRComputeShaders.TraceThreadGroupSize;

            context.cmd.SetComputeMatrixParam(_traceCS, ShaderID._SSR_ViewProjMatrix, data.ViewProjMatrix);
            context.cmd.SetComputeMatrixParam(_traceCS, ShaderID._SSR_InvViewProjMatrix, data.InvViewProjMatrix);
            context.cmd.SetComputeVectorParam(_traceCS, ShaderID._SSR_CameraPosition, data.CameraPosition);
            context.cmd.SetComputeVectorParam(_traceCS, ShaderID._SSR_ScreenSize, data.ScreenSizePixels);

            context.cmd.SetComputeTextureParam(_traceCS, kernelIndex, ShaderID._Result, data.Result);

            context.cmd.DispatchCompute(_traceCS, kernelIndex,
                AAAAMathUtils.AlignUp((int) data.ScreenSizePixels.x, threadGroupSize) / threadGroupSize,
                AAAAMathUtils.AlignUp((int) data.ScreenSizePixels.y, threadGroupSize) / threadGroupSize,
                1
            );
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderID
        {
            public static readonly int _SSR_ViewProjMatrix = Shader.PropertyToID("_SSR_ViewProjMatrix");
            public static readonly int _SSR_InvViewProjMatrix = Shader.PropertyToID("_SSR_InvViewProjMatrix");
            public static readonly int _SSR_CameraPosition = Shader.PropertyToID("_SSR_CameraPosition");
            public static readonly int _SSR_ScreenSize = Shader.PropertyToID("_SSR_ScreenSize");
            public static readonly int _Result = Shader.PropertyToID("_Result");
        }

        public class PassData : PassDataBase
        {
            public Vector4 CameraPosition;
            public Matrix4x4 InvViewProjMatrix;
            public TextureHandle Result;
            public Vector4 ScreenSizePixels;
            public Matrix4x4 ViewProjMatrix;
        }
    }
}