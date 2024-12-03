using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    public class DrawShadowsBatchedPass : AAAARenderPass<DrawShadowsBatchedPass.PassData>
    {
        public DrawShadowsBatchedPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent) { }

        public int ShadowLightIndex { get; set; }
        public int SplitIndex { get; set; }
        public int ContextIndex { get; set; }

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            passData.Reset();

            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAAShadowsData shadowsData = frameData.Get<AAAAShadowsData>();

            NativeList<AAAAShadowsData.ShadowLight> shadowLights = shadowsData.ShadowLights;
            ref readonly AAAAShadowsData.ShadowLight shadowLight = ref shadowLights.ElementAtRef(ShadowLightIndex);
            ref readonly AAAAShadowsData.ShadowLightSplit shadowLightSplit = ref shadowLight.Splits.ElementAtRef(SplitIndex);

            passData.SlopeBias = shadowLight.SlopeBias;
            Vector4 shadowLightDirection;

            if (shadowLight.LightType == LightType.Directional)
            {
                shadowLightDirection = -shadowLightSplit.CullingView.CameraForward;
                shadowLightDirection.w = 0.0f;
            }
            else
            {
                shadowLightDirection = shadowLightSplit.CullingView.CameraPosition;
                shadowLightDirection.w = 1.0f;
            }

            passData.ShadowRenderingConstantBuffer = new AAAAShadowRenderingConstantBuffer
            {
                ShadowViewMatrix = shadowLightSplit.CullingView.ViewMatrix,
                ShadowProjectionMatrix = shadowLightSplit.GPUProjectionMatrix,
                ShadowViewProjection = shadowLightSplit.CullingView.GPUViewProjectionMatrix,
                ShadowLightDirection = shadowLightDirection,
                ShadowBiases = new Vector4(shadowLight.DepthBias, 0, 0, 0),
            };
            passData.RendererContainer = renderingData.RendererContainer;
            passData.CameraType = cameraData.CameraType;
            passData.ZClip = shadowLightSplit.CullingView.IsPerspective ? 1.0f : 0.0f;

            AAAARenderTexturePool shadowMapPool = renderingData.RtPoolSet.ShadowMap;
            RenderTexture shadowMap = shadowMapPool.LookupRenderTexture(shadowLightSplit.ShadowMapAllocation);
            passData.ShadowMap = shadowMap;

            if (shadowLightSplit.RsmAttachmentAllocation.IsValid)
            {
                renderingData.RtPoolSet.LookupRsmAttachments(shadowLightSplit.RsmAttachmentAllocation, passData.RsmAttachments);
                passData.UseRsm = true;
            }

            builder.AllowPassCulling(false);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            using var _ = new ProfilingScope(context.cmd, Profiling.GetShadowLightPassSampler(ShadowLightIndex, SplitIndex));

            if (data.UseRsm)
            {
                context.cmd.SetRenderTarget(data.RsmAttachments, data.ShadowMap);
                CoreUtils.SetKeyword(context.cmd, AAAARenderPipelineCore.ShaderKeywordStrings.LPV_REFLECTIVE_SHADOW_MAPS, true);
            }
            else
            {
                context.cmd.SetRenderTarget(data.ShadowMap);
            }

            context.cmd.ClearRenderTarget(RTClearFlags.Depth, Color.clear, 1.0f, 0);

            // these values match HDRP defaults (see https://github.com/Unity-Technologies/Graphics/blob/9544b8ed2f98c62803d285096c91b44e9d8cbc47/com.unity.render-pipelines.high-definition/Runtime/Lighting/Shadow/HDShadowAtlas.cs#L197 )
            context.cmd.SetGlobalDepthBias(1.0f, data.SlopeBias);
            context.cmd.SetGlobalFloat(ShaderIDs._ZClip, data.ZClip);

            ConstantBuffer.PushGlobal(context.cmd, data.ShadowRenderingConstantBuffer, ShaderIDs.ShadowRenderingConstantBuffer);
            data.RendererContainer.Draw(data.CameraType, context.cmd, AAAARendererContainer.PassType.ShadowCaster, ContextIndex);

            context.cmd.SetGlobalDepthBias(0.0f, 0.0f);

            if (data.UseRsm)
            {
                CoreUtils.SetKeyword(context.cmd, AAAARenderPipelineCore.ShaderKeywordStrings.LPV_REFLECTIVE_SHADOW_MAPS, false);
            }
        }

        private static class Profiling
        {
            private static readonly Dictionary<(int, int), ProfilingSampler> ProfilingSamplersCache = new();

            public static ProfilingSampler GetShadowLightPassSampler(int shadowLightIndex, int splitIndex)
            {
                (int, int) key = (shadowLightIndex, splitIndex);
                if (!ProfilingSamplersCache.TryGetValue(key, out ProfilingSampler profilingSampler))
                {
                    ProfilingSamplersCache[key] = profilingSampler = new ProfilingSampler($"ShadowLightPass_{shadowLightIndex:0000}:{splitIndex}");
                }

                return profilingSampler;
            }
        }

        public class PassData : PassDataBase
        {
            public readonly RenderTargetIdentifier[] RsmAttachments = new RenderTargetIdentifier[AAAALightPropagationVolumes.AttachmentsCount];
            public CameraType CameraType;
            public AAAARendererContainer RendererContainer;
            public RenderTargetIdentifier ShadowMap;
            public AAAAShadowRenderingConstantBuffer ShadowRenderingConstantBuffer;
            public float SlopeBias;
            public bool UseRsm;
            public float ZClip;

            public void Reset()
            {
                for (int index = 0; index < RsmAttachments.Length; index++)
                {
                    RsmAttachments[index] = default;
                }
                UseRsm = false;
            }
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static readonly int ShadowRenderingConstantBuffer = Shader.PropertyToID(nameof(AAAAShadowRenderingConstantBuffer));
            public static readonly int _ZClip = Shader.PropertyToID(nameof(_ZClip));
        }
    }
}