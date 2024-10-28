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
    public class DrawShadowsPass : AAAARenderPass<DrawShadowsPass.PassData>
    {
        public DrawShadowsPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent) { }

        public int ShadowLightIndex { get; set; }
        public int SplitIndex { get; set; }
        public int ContextIndex { get; set; }

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAAShadowsData shadowsData = frameData.Get<AAAAShadowsData>();

            NativeList<AAAAShadowsData.ShadowLight> shadowLights = shadowsData.ShadowLights;
            ref readonly AAAAShadowsData.ShadowLight shadowLight = ref shadowLights.ElementAtRef(ShadowLightIndex);
            ref readonly AAAAShadowsData.ShadowLightSplit shadowLightSplit = ref shadowLight.Splits.ElementAtRef(SplitIndex);

            passData.SlopeBias = shadowLight.SlopeBias;
            passData.ShadowRenderingConstantBuffer = new AAAAShadowRenderingConstantBuffer
            {
                ShadowViewMatrix = shadowLightSplit.CullingView.ViewMatrix,
                ShadowProjectionMatrix = shadowLightSplit.GPUProjectionMatrix,
                ShadowViewProjection = shadowLightSplit.CullingView.GPUViewProjectionMatrix,
            };
            passData.RendererContainer = renderingData.RendererContainer;
            passData.CameraType = cameraData.CameraType;

            RenderTexture shadowMap = shadowsData.ShadowMapPool.LookupRenderTexture(shadowLightSplit.ShadowMapAllocation);
            passData.ShadowMap = shadowMap;
            builder.AllowPassCulling(false);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            context.cmd.SetRenderTarget(data.ShadowMap);
            context.cmd.ClearRenderTarget(RTClearFlags.Depth, Color.clear, 1.0f, 0);

            // these values match HDRP defaults (see https://github.com/Unity-Technologies/Graphics/blob/9544b8ed2f98c62803d285096c91b44e9d8cbc47/com.unity.render-pipelines.high-definition/Runtime/Lighting/Shadow/HDShadowAtlas.cs#L197 )
            context.cmd.SetGlobalDepthBias(1.0f, data.SlopeBias);

            ConstantBuffer.PushGlobal(context.cmd, data.ShadowRenderingConstantBuffer, ShaderIDs.ShadowRenderingConstantBuffer);
            data.RendererContainer.Draw(data.CameraType, context.cmd, AAAARendererContainer.PassType.ShadowCaster, ContextIndex);

            context.cmd.SetGlobalDepthBias(0.0f, 0.0f);
        }

        public class PassData : PassDataBase
        {
            public CameraType CameraType;
            public AAAARendererContainer RendererContainer;
            public RenderTexture ShadowMap;
            public AAAAShadowRenderingConstantBuffer ShadowRenderingConstantBuffer;
            public float SlopeBias;
        }

        private static class ShaderIDs
        {
            public static readonly int ShadowRenderingConstantBuffer = Shader.PropertyToID(nameof(AAAAShadowRenderingConstantBuffer));
        }
    }
}