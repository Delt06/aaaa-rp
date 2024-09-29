using System;
using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Data;
using DELTation.AAAARP.FrameData;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.AntiAliasing
{
    public sealed class SMAAPass : AAAARenderPass<SMAAPass.PassData>, IDisposable
    {
        private readonly Texture2D _areaTex;
        private readonly Material _material;
        private readonly MaterialPropertyBlock _propertyBlock = new();
        private readonly Texture2D _searchTex;
        private readonly LocalKeyword _presetHighKeyword;
        private readonly LocalKeyword _presetLowKeyword;
        private readonly LocalKeyword _presetMediumKeyword;
        private readonly LocalKeyword _presetUltraKeyword;

        public SMAAPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders shaders,
            AAAARenderPipelineRuntimeTextures runtimeTextures) : base(renderPassEvent)
        {
            _material = CoreUtils.CreateEngineMaterial(shaders.SMAAPS);
            _presetLowKeyword = new LocalKeyword(_material.shader, "SMAA_PRESET_LOW");
            _presetMediumKeyword = new LocalKeyword(_material.shader, "SMAA_PRESET_MEDIUM");
            _presetHighKeyword = new LocalKeyword(_material.shader, "SMAA_PRESET_HIGH");
            _presetUltraKeyword = new LocalKeyword(_material.shader, "SMAA_PRESET_ULTRA");
            _searchTex = runtimeTextures.SMAASearchTex;
            _areaTex = runtimeTextures.SMAAAreaTex;
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
        }

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();

            TextureDesc cameraColorDesc = resourceData.CameraScaledColorDesc;

            passData.Preset = renderingData.PipelineAsset.ImageQualitySettings.SMAA.Preset;

            {
                passData.ColorBuffer = builder.ReadWriteTexture(resourceData.CameraScaledColorBuffer);
            }

            {
                TextureDesc edgesDesc = cameraColorDesc;
                edgesDesc.name = "SMAAEdges";
                edgesDesc.clearBuffer = true;
                edgesDesc.clearColor = Color.clear;

                // We only need R8G8_UNorm, but using the full format results in better render graph texture reuse.
                edgesDesc.colorFormat = GraphicsFormat.R8G8B8A8_UNorm;

                passData.Edges = builder.CreateTransientTexture(edgesDesc);
            }

            {
                TextureDesc edgesDepthDesc = cameraColorDesc;
                edgesDepthDesc.name = "SMAAEdgesDepth";
                edgesDepthDesc.clearBuffer = true;
                edgesDepthDesc.colorFormat = GraphicsFormat.None;
                edgesDepthDesc.depthBufferBits = DepthBits.Depth32;

                passData.EdgeDepth = builder.CreateTransientTexture(edgesDepthDesc);
            }

            {
                TextureDesc weightsDesc = cameraColorDesc;
                weightsDesc.name = "SMAAWeights";
                weightsDesc.clearBuffer = true;
                weightsDesc.clearColor = Color.clear;
                weightsDesc.colorFormat = GraphicsFormat.R8G8B8A8_UNorm;

                passData.Weights = builder.CreateTransientTexture(weightsDesc);
            }

            {
                TextureDesc targetDesc = cameraColorDesc;
                targetDesc.name = "SMAATarget";

                passData.Target = builder.CreateTransientTexture(targetDesc);
            }
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            _propertyBlock.Clear();
            _propertyBlock.SetVector(ShaderIDs._BlitScaleBias, new Vector4(1, 1, 0, 0));

            context.cmd.SetKeyword(_material, _presetLowKeyword, data.Preset == AAAAImageQualitySettings.SMAAPreset.Low);
            context.cmd.SetKeyword(_material, _presetMediumKeyword, data.Preset == AAAAImageQualitySettings.SMAAPreset.Medium);
            context.cmd.SetKeyword(_material, _presetHighKeyword, data.Preset == AAAAImageQualitySettings.SMAAPreset.High);
            context.cmd.SetKeyword(_material, _presetUltraKeyword, data.Preset == AAAAImageQualitySettings.SMAAPreset.Ultra);

            using (new ProfilingScope(context.cmd, Profiling.EdgeDetection))
            {
                context.cmd.SetRenderTarget(data.Edges, data.EdgeDepth);

                _propertyBlock.SetTexture(ShaderIDs._BlitTexture, data.ColorBuffer);

                const int edgeDetectionPass = 0;
                CustomBlitWithPropertyBlock(context.cmd, edgeDetectionPass);
            }

            using (new ProfilingScope(context.cmd, Profiling.BlendingWeightsCalculation))
            {
                context.cmd.SetRenderTarget(data.Weights, data.EdgeDepth);

                _propertyBlock.SetTexture(ShaderIDs._EdgesTex, data.Edges);
                _propertyBlock.SetTexture(ShaderIDs._AreaTex, _areaTex);
                _propertyBlock.SetTexture(ShaderIDs._SearchTex, _searchTex);

                const int blendingWeightsCalculationPass = 1;
                CustomBlitWithPropertyBlock(context.cmd, blendingWeightsCalculationPass);
            }

            using (new ProfilingScope(context.cmd, Profiling.NeighborhoodBlending))
            {
                context.cmd.SetRenderTarget(data.Target);

                _propertyBlock.SetTexture(ShaderIDs._BlendTex, data.Weights);

                const int neighborhoodBlendingPass = 2;
                CustomBlitWithPropertyBlock(context.cmd, neighborhoodBlendingPass);
            }

            using (new ProfilingScope(context.cmd, Profiling.CopyToColorBuffer))
            {
                context.cmd.CopyTexture(data.Target, data.ColorBuffer);
            }
        }

        private void CustomBlitWithPropertyBlock(CommandBuffer cmd, int passIndex)
        {
            cmd.DrawProcedural(Matrix4x4.identity, _material, passIndex, MeshTopology.Triangles, 3, 1, _propertyBlock);
        }

        public class PassData : PassDataBase
        {
            public TextureHandle ColorBuffer;
            public TextureHandle EdgeDepth;
            public TextureHandle Edges;
            public AAAAImageQualitySettings.SMAAPreset Preset;
            public TextureHandle Target;
            public TextureHandle Weights;
        }

        private static class Profiling
        {
            public static readonly ProfilingSampler EdgeDetection = new(nameof(EdgeDetection));
            public static readonly ProfilingSampler BlendingWeightsCalculation = new(nameof(BlendingWeightsCalculation));
            public static readonly ProfilingSampler NeighborhoodBlending = new(nameof(NeighborhoodBlending));
            public static readonly ProfilingSampler CopyToColorBuffer = new(nameof(CopyToColorBuffer));
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static readonly int _BlitTexture = Shader.PropertyToID(nameof(_BlitTexture));
            public static readonly int _BlitScaleBias = Shader.PropertyToID(nameof(_BlitScaleBias));

            public static readonly int _EdgesTex = Shader.PropertyToID(nameof(_EdgesTex));
            public static readonly int _AreaTex = Shader.PropertyToID(nameof(_AreaTex));
            public static readonly int _SearchTex = Shader.PropertyToID(nameof(_SearchTex));

            public static readonly int _BlendTex = Shader.PropertyToID(nameof(_BlendTex));
        }
    }
}