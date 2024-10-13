using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Meshlets;
using DELTation.AAAARP.RenderPipelineResources;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes
{
    public sealed class HZBGenerationPass : AAAARenderPass<HZBGenerationPass.PassData>
    {
        public enum Mode
        {
            Min,
            Max,
        }

        private readonly ComputeShader _hzbGenerationCS;
        private readonly Mode _mode;

        public HZBGenerationPass(AAAARenderPassEvent renderPassEvent, Mode mode, string passNamePrefix, AAAARenderPipelineRuntimeShaders shaders) :
            base(renderPassEvent)
        {
            _hzbGenerationCS = shaders.HZBGenerationCS;
            _mode = mode;
            Name = passNamePrefix + AutoName;
        }

        public override string Name { get; }

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();

            passData.CameraDepth = builder.ReadTexture(resourceData.CameraScaledDepthBuffer);
            passData.HZB = builder.WriteTexture(resourceData.CameraHZBScaled);
            passData.HZBInfo = resourceData.CameraScaledHZBInfo;
            passData.Mode = _mode;
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            const int kernelIndex = 0;
            GenerateHZB(data, context, _hzbGenerationCS, kernelIndex);
        }

        private static void GenerateHZB(PassData data, RenderGraphContext context, ComputeShader cs, int kernelIndex)
        {
            if (FrameDebugger.enabled)
            {
                context.cmd.SetRenderTarget(data.HZB);
            }

            CoreUtils.SetKeyword(context.cmd, cs, "USE_MAX", data.Mode == Mode.Max);

            for (int i = 1; i < data.HZBInfo.LevelCount; i++)
            {
                var destRect = (int4) (float4) data.HZBInfo.MipRects[i];
                var srcRect = (int4) (float4) data.HZBInfo.MipRects[i - 1];

                data.HZBSrcOffset[0] = srcRect.x;
                data.HZBSrcOffset[1] = srcRect.y;
                data.HZBSrcOffset[2] = srcRect.x + srcRect.z - 1;
                data.HZBSrcOffset[3] = srcRect.y + srcRect.w - 1;

                data.HZBDstOffset[0] = destRect.x;
                data.HZBDstOffset[1] = destRect.y;
                data.HZBDstOffset[2] = 0;
                data.HZBDstOffset[3] = 0;

                context.cmd.SetComputeVectorParam(cs, ShaderID.HZBGeneration._DimensionsRatio,
                    new Vector4((float)srcRect.z / destRect.z, (float)srcRect.w / destRect.z, 0.0f, 0.0f)
                );
                context.cmd.SetComputeIntParams(cs, ShaderID.HZBGeneration._SrcOffsetAndLimit, data.HZBSrcOffset);
                context.cmd.SetComputeIntParams(cs, ShaderID.HZBGeneration._DstOffset, data.HZBDstOffset);
                int2 textureSize = data.HZBInfo.TextureSize;
                context.cmd.SetComputeVectorParam(cs, ShaderID.HZBGeneration._HZBTextureSize, new Vector4(textureSize.x, textureSize.y));
                context.cmd.SetComputeTextureParam(cs, kernelIndex, ShaderID.HZBGeneration._HZB, data.HZB);

                CoreUtils.SetKeyword(context.cmd, cs, "MIP_1", i == 1);
                if (i == 1)
                {
                    context.cmd.SetComputeTextureParam(cs, kernelIndex, ShaderID.HZBGeneration._CameraDepth, data.CameraDepth);
                }

                const int threadGroupSizeX = (int) AAAAMeshletComputeShaders.HZBGenerationThreadGroupSizeX;
                const int threadGroupSizeY = (int) AAAAMeshletComputeShaders.HZBGenerationThreadGroupSizeY;
                int threadGroupsX = AAAAMathUtils.AlignUp(destRect.z, threadGroupSizeX) / threadGroupSizeX;
                int threadGroupsY = AAAAMathUtils.AlignUp(destRect.w, threadGroupSizeY) / threadGroupSizeY;
                const int threadGroupsZ = 1;
                context.cmd.DispatchCompute(cs, kernelIndex, threadGroupsX, threadGroupsY, threadGroupsZ);
            }

            context.cmd.SetGlobalTexture(ShaderID._CameraHZB, data.HZB);
            context.cmd.SetGlobalVectorArray(ShaderID._CameraHZBMipRects, data.HZBInfo.MipRects);
            context.cmd.SetGlobalInt(ShaderID._CameraHZBLevelCount, data.HZBInfo.LevelCount);
        }

        public class PassData : PassDataBase
        {
            public readonly int[] HZBDstOffset = new int[4];
            public readonly int[] HZBSrcOffset = new int[4];
            public TextureHandle CameraDepth;
            public TextureHandle HZB;
            public AAAAResourceData.HZBInfo HZBInfo;
            public Mode Mode;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderID
        {
            public static readonly int _CameraHZB = Shader.PropertyToID(nameof(_CameraHZB));
            public static readonly int _CameraHZBMipRects = Shader.PropertyToID(nameof(_CameraHZBMipRects));
            public static readonly int _CameraHZBLevelCount = Shader.PropertyToID(nameof(_CameraHZBLevelCount));

            public static class HZBGeneration
            {
                public static readonly int _HZBTextureSize = Shader.PropertyToID(nameof(_HZBTextureSize));
                public static readonly int _DimensionsRatio = Shader.PropertyToID(nameof(_DimensionsRatio));
                public static readonly int _SrcOffsetAndLimit = Shader.PropertyToID(nameof(_SrcOffsetAndLimit));
                public static readonly int _DstOffset = Shader.PropertyToID(nameof(_DstOffset));

                public static readonly int _CameraDepth = Shader.PropertyToID(nameof(_CameraDepth));
                public static readonly int _HZB = Shader.PropertyToID(nameof(_HZB));
            }
        }
    }
}