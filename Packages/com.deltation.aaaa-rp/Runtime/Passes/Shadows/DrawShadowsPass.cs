using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Lighting;
using DELTation.AAAARP.Renderers;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.Shadows
{
    public class DrawShadowsPass : AAAARasterRenderPass<DrawShadowsPass.PassData>
    {
        public DrawShadowsPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent) { }

        public int ShadowLightIndex { get; set; }

        protected override void Setup(IRasterRenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAAShadowsData shadowsData = frameData.Get<AAAAShadowsData>();

            NativeList<AAAAShadowsData.ShadowLight> shadowLights = shadowsData.ShadowLights;
            ref readonly AAAAShadowsData.ShadowLight shadowLight = ref shadowLights.ElementAtRef(ShadowLightIndex);

            passData.SlopeBias = shadowLight.SlopeBias;
            passData.ShadowRenderingConstantBuffer = new AAAAShadowRenderingConstantBuffer
            {
                ShadowViewMatrix = shadowLight.ViewMatrix,
                ShadowProjectionMatrix = shadowLight.GPUProjectionMatrix,
                ShadowViewProjection = shadowLight.CullingView.GPUViewProjectionMatrix,
            };
            passData.RendererContainer = renderingData.RendererContainer;
            passData.CameraType = cameraData.CameraType;

            TextureHandle shadowMap = shadowsData.DirectionalLightShadowMapArray;
            builder.SetRenderAttachmentDepth(shadowMap, AccessFlags.WriteAll, 0, ShadowLightIndex);
            builder.AllowPassCulling(false);
        }

        protected override void Render(PassData data, RasterGraphContext context)
        {
            // these values match HDRP defaults (see https://github.com/Unity-Technologies/Graphics/blob/9544b8ed2f98c62803d285096c91b44e9d8cbc47/com.unity.render-pipelines.high-definition/Runtime/Lighting/Shadow/HDShadowAtlas.cs#L197 )
            context.cmd.SetGlobalDepthBias(1.0f, data.SlopeBias);

            context.cmd.ClearRenderTarget(RTClearFlags.Depth, Color.clear, 1.0f, 0);

            ConstantBuffer.PushGlobal(context.cmd.m_WrappedCommandBuffer, data.ShadowRenderingConstantBuffer, ShaderIDs.ShadowRenderingConstantBuffer);
            data.RendererContainer.Draw(data.CameraType, context.cmd, AAAARendererContainer.PassType.ShadowCaster);

            context.cmd.SetGlobalDepthBias(0.0f, 0.0f);
        }

        public class PassData : PassDataBase
        {
            public float SlopeBias;
            public CameraType CameraType;
            public AAAARendererContainer RendererContainer;
            public AAAAShadowRenderingConstantBuffer ShadowRenderingConstantBuffer;
        }

        private static class ShaderIDs
        {
            public static readonly int ShadowRenderingConstantBuffer = Shader.PropertyToID(nameof(AAAAShadowRenderingConstantBuffer));
        }
    }
}