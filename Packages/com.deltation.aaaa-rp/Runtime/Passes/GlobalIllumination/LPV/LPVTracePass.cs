using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Utils;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.GlobalIllumination.LPV
{
    public class LPVTracePass : AAAARenderPass<LPVTracePass.PassData>
    {
        private readonly Material _material;

        public LPVTracePass(AAAARenderPassEvent renderPassEvent, Material material) : base(renderPassEvent) => _material = material;

        public override string Name => "LPV.Trace";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            AAAALightingData lightingData = frameData.Get<AAAALightingData>();

            const float resolutionScale = 1.0f;
            int targetWidth = (int) math.ceil(cameraData.ScaledWidth * resolutionScale);
            int targetHeight = (int) math.ceil(cameraData.ScaledHeight * resolutionScale);

            {
                const bool isHdrEnabled = true;
                const bool needsAlpha = false;
                GraphicsFormat colorFormat = AAAARenderPipelineCore.MakeRenderTextureGraphicsFormat(isHdrEnabled, HDRColorBufferPrecision._32Bits, needsAlpha);
                TextureDesc textureDesc = AAAARenderingUtils.CreateTextureDesc("LPVTraceResult",
                    new RenderTextureDescriptor(targetWidth, targetHeight, colorFormat, GraphicsFormat.None)
                );
                textureDesc.clearBuffer = true;
                textureDesc.clearColor = Color.clear;
                passData.Result = lightingData.LPVTraceResult = renderingData.RenderGraph.CreateTexture(textureDesc);
            }

            builder.ReadTexture(resourceData.CameraScaledDepthBuffer);
            builder.ReadTexture(resourceData.GBufferNormals);
            builder.WriteTexture(passData.Result);

            builder.AllowPassCulling(false);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            context.cmd.SetRenderTarget(data.Result);

            {
                MaterialPropertyBlock propertyBlock = data.MaterialPropertyBlock;

                propertyBlock.Clear();
                propertyBlock.SetVector(ShaderIDs._BlitScaleBias, new Vector4(1, 1, 0, 0));

                const int shaderPassId = 0;
                AAAABlitter.BlitTriangle(context.cmd, _material, shaderPassId, propertyBlock);
            }

            context.cmd.SetGlobalTexture(ShaderIDs.Global._LPVTraceResult, data.Result);
        }

        public class PassData : PassDataBase
        {
            public readonly MaterialPropertyBlock MaterialPropertyBlock = new();
            public TextureHandle Result;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static readonly int _BlitScaleBias = Shader.PropertyToID(nameof(_BlitScaleBias));

            public static class Global
            {
                public static readonly int _LPVTraceResult = Shader.PropertyToID(nameof(_LPVTraceResult));
            }
        }
    }
}