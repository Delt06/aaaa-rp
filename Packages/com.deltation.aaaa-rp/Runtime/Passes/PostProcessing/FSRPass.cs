using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.Data;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using DELTation.AAAARP.Shaders.PostProcessing.FSR;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using AU1 = System.UInt32;
using AU4 = Unity.Mathematics.uint4;
using AF1 = System.Single;
using AF2 = Unity.Mathematics.float2;

namespace DELTation.AAAARP.Passes.PostProcessing
{
    public sealed class FSRPass : AAAARenderPass<FSRPass.PassData>
    {
        private readonly ComputeShader _easuCS;
        private readonly ComputeShader _rcasCS;

        public FSRPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent)
        {
            AAAAFsrRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAAFsrRuntimeShaders>();
            _easuCS = shaders.EasuCS;
            _rcasCS = shaders.RcasCS;
        }

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();

            passData.Input = builder.ReadTexture(resourceData.CameraScaledColorBuffer);
            passData.InputResolution = math.int2(cameraData.ScaledWidth, cameraData.ScaledHeight);
            passData.Output = builder.WriteTexture(resourceData.CameraColorBuffer);
            passData.OutputResolution = math.int2(cameraData.PixelWidth, cameraData.PixelHeight);
            passData.HDR = cameraData.IsHdrEnabled;
            passData.Sharpness = cameraData.FSRSharpness;
            passData.UseRCAS = passData.Sharpness > 0.0f;

            if (passData.UseRCAS)
            {
                TextureDesc textureDesc = resourceData.CameraColorDesc;
                textureDesc.name = nameof(PassData.Intermedate);
                passData.Intermedate = builder.CreateTransientTexture(textureDesc);
            }
            else
            {
                passData.Intermedate = passData.Output;
            }
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            const int threadGroupSizeX = AAAAFSRConstantBuffer.ThreadGroupSizeX;
            int threadGroupsX = AAAAMathUtils.AlignUp(data.OutputResolution.x, threadGroupSizeX) / threadGroupSizeX;
            const int threadGroupSizeY = AAAAFSRConstantBuffer.ThreadGroupSizeY;
            int threadGroupsY = AAAAMathUtils.AlignUp(data.OutputResolution.y, threadGroupSizeY) / threadGroupSizeY;

            using (new ProfilingScope(context.cmd, Profiling.EASU))
            {
                AAAAFSRConstantBuffer constantBuffer = default;
                Utils.FsrEasuCon(
                    out constantBuffer.Const0, out constantBuffer.Const1, out constantBuffer.Const2, out constantBuffer.Const3,
                    data.InputResolution.x, data.InputResolution.y,
                    data.InputResolution.x, data.InputResolution.y,
                    data.OutputResolution.x, data.OutputResolution.y
                );

                //constantBuffer.Sample.x = data.HDR && !data.UseRCAS ? 1u : 0u;
                ConstantBuffer.Push(context.cmd, constantBuffer, _easuCS, ShaderIDs.ConstantBuffer);

                const int kernelIndex = 0;
                context.cmd.SetComputeTextureParam(_easuCS, kernelIndex, ShaderIDs._InputTexture, data.Input);
                context.cmd.SetComputeTextureParam(_easuCS, kernelIndex, ShaderIDs._OutputTexture, data.Intermedate);
                context.cmd.DispatchCompute(_easuCS, kernelIndex, threadGroupsX, threadGroupsY, 1);
            }

            if (data.UseRCAS)
            {
                using (new ProfilingScope(context.cmd, Profiling.RCAS))
                {
                    AAAAFSRConstantBuffer constantBuffer = default;
                    Utils.FsrRcasCon(
                        out constantBuffer.Const0,
                        AAAAImageQualitySettings.MaxFSRSharpness - data.Sharpness
                    );

                    //constantBuffer.Sample.x = data.HDR ? 1u : 0u;
                    ConstantBuffer.Push(context.cmd, constantBuffer, _rcasCS, ShaderIDs.ConstantBuffer);

                    const int kernelIndex = 0;
                    context.cmd.SetComputeTextureParam(_rcasCS, kernelIndex, ShaderIDs._InputTexture, data.Intermedate);
                    context.cmd.SetComputeTextureParam(_rcasCS, kernelIndex, ShaderIDs._OutputTexture, data.Output);
                    context.cmd.DispatchCompute(_rcasCS, kernelIndex, threadGroupsX, threadGroupsY, 1);
                }
            }
        }

        public class PassData : PassDataBase
        {
            public bool HDR;
            public TextureHandle Input;
            public int2 InputResolution;
            public TextureHandle Intermedate;
            public TextureHandle Output;
            public int2 OutputResolution;
            public float Sharpness;
            public bool UseRCAS;
        }

        private static class Profiling
        {
            public static readonly ProfilingSampler EASU = new(nameof(EASU));
            public static readonly ProfilingSampler RCAS = new(nameof(RCAS));
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static readonly int _InputTexture = Shader.PropertyToID(nameof(_InputTexture));
            public static readonly int _OutputTexture = Shader.PropertyToID(nameof(_OutputTexture));
            public static readonly int ConstantBuffer = Shader.PropertyToID(nameof(AAAAFSRConstantBuffer));
        }

        private static class Utils
        {
            private static AU1 AU1_AF1(AF1 x) => math.asuint(x);

            private static AF1 ARcpF1(AF1 x) => math.rcp(x);

            private static AF1 AF1_(double x) => (AF1) x;

            private static AF1 AExp2F1(float x) => math.exp2(x);

            // ReSharper disable once InconsistentNaming
            private static AF2 initAF2(AF1 x, AF1 y) => math.float2(x, y);

            private static AU1 AU1_AH2_AF2(AF2 a) => math.half(a[0]).value + ((AU1) math.half(a[1]).value << 16);

            public static void FsrEasuCon(
                out AU4 con0,
                out AU4 con1,
                out AU4 con2,
                out AU4 con3,

                // This the rendered image resolution being upscaled
                AF1 inputViewportInPixelsX,
                AF1 inputViewportInPixelsY,

                // This is the resolution of the resource containing the input image (useful for dynamic resolution)
                AF1 inputSizeInPixelsX,
                AF1 inputSizeInPixelsY,

                // This is the display resolution which the input image gets upscaled to
                AF1 outputSizeInPixelsX,
                AF1 outputSizeInPixelsY)
            {
                con0 = default;
                con1 = default;
                con2 = default;
                con3 = default;

                // Output integer position to a pixel position in viewport.
                con0[0] = AU1_AF1(inputViewportInPixelsX * ARcpF1(outputSizeInPixelsX));
                con0[1] = AU1_AF1(inputViewportInPixelsY * ARcpF1(outputSizeInPixelsY));
                con0[2] = AU1_AF1(AF1_(0.5) * inputViewportInPixelsX * ARcpF1(outputSizeInPixelsX) - AF1_(0.5));
                con0[3] = AU1_AF1(AF1_(0.5) * inputViewportInPixelsY * ARcpF1(outputSizeInPixelsY) - AF1_(0.5));

                // Viewport pixel position to normalized image space.
                // This is used to get upper-left of 'F' tap.
                con1[0] = AU1_AF1(ARcpF1(inputSizeInPixelsX));
                con1[1] = AU1_AF1(ARcpF1(inputSizeInPixelsY));

                // Centers of gather4, first offset from upper-left of 'F'.
                //      +---+---+
                //      |   |   |
                //      +--(0)--+
                //      | b | c |
                //  +---F---+---+---+
                //  | e | f | g | h |
                //  +--(1)--+--(2)--+
                //  | i | j | k | l |
                //  +---+---+---+---+
                //      | n | o |
                //      +--(3)--+
                //      |   |   |
                //      +---+---+
                con1[2] = AU1_AF1(AF1_(1.0) * ARcpF1(inputSizeInPixelsX));
                con1[3] = AU1_AF1(AF1_(-1.0) * ARcpF1(inputSizeInPixelsY));

                // These are from (0) instead of 'F'.
                con2[0] = AU1_AF1(AF1_(-1.0) * ARcpF1(inputSizeInPixelsX));
                con2[1] = AU1_AF1(AF1_(2.0) * ARcpF1(inputSizeInPixelsY));
                con2[2] = AU1_AF1(AF1_(1.0) * ARcpF1(inputSizeInPixelsX));
                con2[3] = AU1_AF1(AF1_(2.0) * ARcpF1(inputSizeInPixelsY));
                con3[0] = AU1_AF1(AF1_(0.0) * ARcpF1(inputSizeInPixelsX));
                con3[1] = AU1_AF1(AF1_(4.0) * ARcpF1(inputSizeInPixelsY));
                con3[2] = con3[3] = 0;
            }

            public static void FsrRcasCon(
                out AU4 con,

                // The scale is {0.0 := maximum, to N>0, where N is the number of stops (halving) of the reduction of sharpness}.
                AF1 sharpness)
            {
                con = default;

                // Transform from stops to linear value.
                sharpness = AExp2F1(-sharpness);
                AF2 hSharp = initAF2(sharpness, sharpness);
                con[0] = AU1_AF1(sharpness);
                con[1] = AU1_AH2_AF2(hSharp);
                con[2] = 0;
                con[3] = 0;
            }
        }
    }
}