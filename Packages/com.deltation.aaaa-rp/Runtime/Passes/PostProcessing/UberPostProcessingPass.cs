﻿using System;
using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using DELTation.AAAARP.Utils;
using DELTation.AAAARP.Volumes;
using Unity.Mathematics;
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
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();

            passData.CameraColor = builder.ReadWriteTexture(resourceData.CameraScaledColorBuffer);

            TextureDesc tempTargetDesc = resourceData.CameraScaledColorDesc;
            tempTargetDesc.name = "UberPost_TempTarget";
            passData.TempTarget = builder.CreateTransientTexture(tempTargetDesc);

            AAAAPostProcessingOptionsVolumeComponent postProcessingOptions = cameraData.VolumeStack.GetComponent<AAAAPostProcessingOptionsVolumeComponent>();
            passData.Exposure = postProcessingOptions.Exposure.value;
            passData.ToneMappingProfile = postProcessingOptions.ToneMappingProfile.value;
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            _propertyBlock.Clear();

            context.cmd.SetRenderTarget(data.TempTarget);

            _propertyBlock.SetTexture(ShaderIDs._BlitTexture, data.CameraColor);
            _propertyBlock.SetVector(ShaderIDs._BlitScaleBias, new Vector4(1, 1, 0, 0));

            _propertyBlock.SetFloat(ShaderIDs._Exposure, 1.0f / math.max(0.001f, data.Exposure));
            context.cmd.SetKeyword(_material, _toneMapNeutralKeyword, data.ToneMappingProfile == AAAAToneMappingProfile.Neutral);
            context.cmd.SetKeyword(_material, _toneMapACESKeyword, data.ToneMappingProfile == AAAAToneMappingProfile.ACES);

            const int shaderPass = 0;
            AAAABlitter.BlitTriangle(context.cmd, _material, shaderPass, _propertyBlock);

            context.cmd.CopyTexture(data.TempTarget, data.CameraColor);
        }

        public class PassData : PassDataBase
        {
            public TextureHandle CameraColor;
            public float Exposure;
            public TextureHandle TempTarget;

            public AAAAToneMappingProfile ToneMappingProfile;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static readonly int _BlitTexture = Shader.PropertyToID(nameof(_BlitTexture));
            public static readonly int _BlitScaleBias = Shader.PropertyToID(nameof(_BlitScaleBias));
            public static readonly int _Exposure = Shader.PropertyToID(nameof(_Exposure));
        }
    }
}