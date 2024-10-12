using System;
using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Data;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using DELTation.AAAARP.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.PostProcessing
{
    public sealed class UberPostProcessingPass : AAAARenderPass<UberPostProcessingPass.PassData>, IDisposable
    {
        private readonly Material _material;
        private readonly MaterialPropertyBlock _propertyBlock = new();
        private readonly LocalKeyword _toneMapACESKeyword;
        private readonly LocalKeyword _toneMapNeutralKeyword;

        public UberPostProcessingPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders shaders) : base(renderPassEvent)
        {
            _material = CoreUtils.CreateEngineMaterial(shaders.UberPostProcessingPS);
            _toneMapNeutralKeyword = new LocalKeyword(_material.shader, "_TONEMAP_NEUTRAL");
            _toneMapACESKeyword = new LocalKeyword(_material.shader, "_TONEMAP_ACES");
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
        }

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();

            passData.CameraColor = builder.ReadWriteTexture(resourceData.CameraScaledColorBuffer);

            TextureDesc tempTargetDesc = resourceData.CameraScaledColorDesc;
            tempTargetDesc.name = "UberPost_TempTarget";
            passData.TempTarget = builder.CreateTransientTexture(tempTargetDesc);

            passData.ToneMappingProfile = renderingData.PipelineAsset.PostProcessingSettings.ToneMapping;
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            _propertyBlock.Clear();

            context.cmd.SetRenderTarget(data.TempTarget);

            _propertyBlock.SetTexture(ShaderIDs._BlitTexture, data.CameraColor);
            _propertyBlock.SetVector(ShaderIDs._BlitScaleBias, new Vector4(1, 1, 0, 0));

            context.cmd.SetKeyword(_material, _toneMapNeutralKeyword, data.ToneMappingProfile == AAAAPostProcessingSettings.ToneMappingProfile.Neutral);
            context.cmd.SetKeyword(_material, _toneMapACESKeyword, data.ToneMappingProfile == AAAAPostProcessingSettings.ToneMappingProfile.ACES);

            const int shaderPass = 0;
            AAAABlitter.BlitTriangle(context.cmd, _material, shaderPass, _propertyBlock);

            context.cmd.CopyTexture(data.TempTarget, data.CameraColor);
        }

        public class PassData : PassDataBase
        {
            public TextureHandle CameraColor;
            public TextureHandle TempTarget;

            public AAAAPostProcessingSettings.ToneMappingProfile ToneMappingProfile;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static readonly int _BlitTexture = Shader.PropertyToID(nameof(_BlitTexture));
            public static readonly int _BlitScaleBias = Shader.PropertyToID(nameof(_BlitScaleBias));
        }
    }
}