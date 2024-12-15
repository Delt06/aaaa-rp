using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Meshlets;
using DELTation.AAAARP.RenderPipelineResources;
using DELTation.AAAARP.Volumes;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.GlobalIllumination.SSR
{
    public class SSRTracePass : AAAARenderPass<SSRTracePass.PassData>
    {
        private readonly ComputeShader _traceCS;

        public SSRTracePass(AAAARenderPassEvent renderPassEvent, AAAASsrRuntimeShaders shaders) : base(renderPassEvent) =>
            _traceCS = shaders.TraceCS;

        public override string Name => "SSR.Trace";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            AAAALightingData lightingData = frameData.Get<AAAALightingData>();

            AAAASsrVolumeComponent ssr = cameraData.VolumeStack.GetComponent<AAAASsrVolumeComponent>();
            float resolutionScale = 1.0f / (float) ssr.Resolution.value;
            int ssrTargetWidth = (int) math.ceil(cameraData.ScaledWidth * resolutionScale);
            int ssrTargetHeight = (int) math.ceil(cameraData.ScaledHeight * resolutionScale);

            lightingData.SSRTraceResultSize = math.int2(ssrTargetWidth, ssrTargetHeight);
            lightingData.SSRTraceResult = renderingData.RenderGraph.CreateTexture(
                AAAARenderingUtils.CreateTextureDesc("SSRTraceResult",
                    new RenderTextureDescriptor(ssrTargetWidth, ssrTargetHeight, GraphicsFormat.R16G16B16A16_UNorm, GraphicsFormat.None)
                    {
                        enableRandomWrite = true,
                    }
                )
            );

            passData.ScreenSizePixels = new Vector4(
                ssrTargetWidth, ssrTargetHeight,
                1.0f / ssrTargetWidth, 1.0f / ssrTargetHeight
            );
            passData.Result = builder.WriteTexture(lightingData.SSRTraceResult);

            Matrix4x4 viewProjMatrix = cameraData.GetGPUProjectionMatrixJittered(true) * cameraData.ViewMatrix;
            passData.ViewProjMatrix = viewProjMatrix;
            passData.InvViewProjMatrix = viewProjMatrix.inverse;
            passData.CameraPosition = cameraData.Camera.transform.position;
            passData.MaxThickness = ssr.MaxThickness.value;

            for (int hzbMipIndex = 0; hzbMipIndex < resourceData.CameraScaledHZBInfo.LevelCount; hzbMipIndex++)
            {
                Vector4 mipRect = resourceData.CameraScaledHZBInfo.MipRects[hzbMipIndex];
                var size = new float2(mipRect.z, mipRect.w);
                if (hzbMipIndex > 0)
                {
                    // When a mip level is not evenly divisible by 2, we round the result to the next higher value.
                    // It means the boundary cells in the next mip level correspond not to 4 of its parent, but 2 or even one.
                    // To keep cell computations correct, it is easier to set cell counts of these levels to fractional values.
                    Vector4 previousMipRect = resourceData.CameraScaledHZBInfo.MipRects[hzbMipIndex - 1];
                    bool2 dimensionsWithHalfPixels = new float2(previousMipRect.z, previousMipRect.w) / size < 2;
                    size = math.select(size, size - 0.5f, dimensionsWithHalfPixels);
                }
                passData.HZBCellCounts[hzbMipIndex] = new Vector4(size.x, size.y);
            }

            builder.ReadTexture(resourceData.CameraScaledDepthBuffer);
            builder.ReadTexture(resourceData.CameraHZBScaled);
            builder.ReadTexture(resourceData.GBufferNormals);
            builder.ReadTexture(resourceData.GBufferMasks);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            const int kernelIndex = 0;
            const int threadGroupSize = SSRComputeShaders.TraceThreadGroupSize;

            context.cmd.SetComputeVectorArrayParam(_traceCS, ShaderID._SSR_HZBCellCounts, data.HZBCellCounts);
            context.cmd.SetComputeMatrixParam(_traceCS, ShaderID._SSR_ViewProjMatrix, data.ViewProjMatrix);
            context.cmd.SetComputeMatrixParam(_traceCS, ShaderID._SSR_InvViewProjMatrix, data.InvViewProjMatrix);
            context.cmd.SetComputeVectorParam(_traceCS, ShaderID._SSR_CameraPosition, data.CameraPosition);
            context.cmd.SetComputeVectorParam(_traceCS, ShaderID._SSR_ScreenSize, data.ScreenSizePixels);
            context.cmd.SetComputeFloatParam(_traceCS, ShaderID._SSR_MaxThickness, data.MaxThickness);

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
            public static readonly int _SSR_HZBCellCounts = Shader.PropertyToID(nameof(_SSR_HZBCellCounts));
            public static readonly int _SSR_ViewProjMatrix = Shader.PropertyToID(nameof(_SSR_ViewProjMatrix));
            public static readonly int _SSR_InvViewProjMatrix = Shader.PropertyToID(nameof(_SSR_InvViewProjMatrix));
            public static readonly int _SSR_CameraPosition = Shader.PropertyToID(nameof(_SSR_CameraPosition));
            public static readonly int _SSR_ScreenSize = Shader.PropertyToID(nameof(_SSR_ScreenSize));
            public static readonly int _SSR_MaxThickness = Shader.PropertyToID(nameof(_SSR_MaxThickness));
            public static readonly int _Result = Shader.PropertyToID(nameof(_Result));
        }

        public class PassData : PassDataBase
        {
            public readonly Vector4[] HZBCellCounts = new Vector4[AAAAMeshletComputeShaders.HZBMaxLevelCount];
            public Vector4 CameraPosition;
            public Matrix4x4 InvViewProjMatrix;
            public float MaxThickness;
            public TextureHandle Result;
            public Vector4 ScreenSizePixels;
            public Matrix4x4 ViewProjMatrix;
        }
    }
}