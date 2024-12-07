﻿using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.GlobalIllumination.LPV
{
    public class LPVInjectPass : AAAARenderPass<LPVInjectPass.PassData>
    {
        private readonly ComputeShader _computeShader;

        public LPVInjectPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders shaders) : base(renderPassEvent) =>
            _computeShader = shaders.LpvInjectCS;

        public override string Name => "LPV.Inject";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAALightingData lightingData = frameData.Get<AAAALightingData>();

            passData.GridSize = lightingData.LPVGridSize = 64;
            var lpvGridSHDesc = new TextureDesc
            {
                width = passData.GridSize,
                height = passData.GridSize,
                slices = passData.GridSize,
                dimension = TextureDimension.Tex3D,
                format = GraphicsFormat.R32G32B32A32_SFloat,
                enableRandomWrite = true,
                filterMode = FilterMode.Trilinear,
                msaaSamples = MSAASamples.None,
                useMipMap = false,
            };
            builder.WriteTexture(passData.GridRedSH = lightingData.LPVGridRedSH =
                renderingData.RenderGraph.CreateTexture(new TextureDesc(lpvGridSHDesc) { name = nameof(AAAALightingData.LPVGridRedSH) })
            );
            builder.WriteTexture(passData.GridGreenSH = lightingData.LPVGridGreenSH =
                renderingData.RenderGraph.CreateTexture(new TextureDesc(lpvGridSHDesc) { name = nameof(AAAALightingData.LPVGridGreenSH) })
            );
            builder.WriteTexture(passData.GridBlueSH = lightingData.LPVGridBlueSH =
                renderingData.RenderGraph.CreateTexture(new TextureDesc(lpvGridSHDesc) { name = nameof(AAAALightingData.LPVGridBlueSH) })
            );

            passData.GridBoundsMin = lightingData.LPVGridBoundsMin = math.float3(-20, -20, -20);
            passData.GridBoundsMax = lightingData.LPVGridBoundsMax = math.float3(20, 20, 20);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            context.cmd.SetGlobalInt(ShaderIDs.Global._LPVGridSize, data.GridSize);
            context.cmd.SetGlobalVector(ShaderIDs.Global._LPVGridBoundsMin, (Vector3) data.GridBoundsMin);
            context.cmd.SetGlobalVector(ShaderIDs.Global._LPVGridBoundsMax, (Vector3) data.GridBoundsMax);
            context.cmd.SetGlobalTexture(ShaderIDs.Global._LPVGridRedSH, data.GridRedSH);
            context.cmd.SetGlobalTexture(ShaderIDs.Global._LPVGridGreenSH, data.GridGreenSH);
            context.cmd.SetGlobalTexture(ShaderIDs.Global._LPVGridBlueSH, data.GridBlueSH);

            context.cmd.SetComputeTextureParam(_computeShader, 0, ShaderIDs._GridRedUAV, data.GridRedSH);
            context.cmd.SetComputeTextureParam(_computeShader, 0, ShaderIDs._GridGreenUAV, data.GridGreenSH);
            context.cmd.SetComputeTextureParam(_computeShader, 0, ShaderIDs._GridBlueUAV, data.GridBlueSH);
            context.cmd.DispatchCompute(_computeShader, 0, data.GridSize, data.GridSize, data.GridSize);
        }

        public class PassData : PassDataBase
        {
            public TextureHandle GridBlueSH;
            public float3 GridBoundsMax;
            public float3 GridBoundsMin;
            public TextureHandle GridGreenSH;
            public TextureHandle GridRedSH;
            public int GridSize;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static readonly int _GridRedUAV = Shader.PropertyToID(nameof(_GridRedUAV));
            public static readonly int _GridGreenUAV = Shader.PropertyToID(nameof(_GridGreenUAV));
            public static readonly int _GridBlueUAV = Shader.PropertyToID(nameof(_GridBlueUAV));

            public static class Global
            {
                public static readonly int _LPVGridRedSH = Shader.PropertyToID(nameof(_LPVGridRedSH));
                public static readonly int _LPVGridGreenSH = Shader.PropertyToID(nameof(_LPVGridGreenSH));
                public static readonly int _LPVGridBlueSH = Shader.PropertyToID(nameof(_LPVGridBlueSH));
                public static readonly int _LPVGridSize = Shader.PropertyToID(nameof(_LPVGridSize));
                public static readonly int _LPVGridBoundsMin = Shader.PropertyToID(nameof(_LPVGridBoundsMin));
                public static readonly int _LPVGridBoundsMax = Shader.PropertyToID(nameof(_LPVGridBoundsMax));
            }
        }
    }
}