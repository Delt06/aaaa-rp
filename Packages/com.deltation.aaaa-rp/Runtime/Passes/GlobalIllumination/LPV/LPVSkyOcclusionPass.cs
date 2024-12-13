using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Lighting;
using DELTation.AAAARP.RenderPipelineResources;
using DELTation.AAAARP.Volumes;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.GlobalIllumination.LPV
{
    public class LPVSkyOcclusionPass : AAAARenderPass<LPVSkyOcclusionPass.PassData>
    {
        private const int KernelIndex = 0;
        private readonly ComputeShader _computeShader;

        public LPVSkyOcclusionPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent)
        {
            AAAALpvRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAALpvRuntimeShaders>();
            _computeShader = shaders.SkyOcclusionCS;
        }

        public override string Name => "LPV.SkyOcclusion";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAALightPropagationVolumesData lpvData = frameData.Get<AAAALightPropagationVolumesData>();
            AAAALPVVolumeComponent volumeComponent = cameraData.VolumeStack.GetComponent<AAAALPVVolumeComponent>();

            lpvData.SkyOcclusionTexture = renderingData.RenderGraph.CreateTexture(new TextureDesc(lpvData.UnpackedGridTextures.SHDesc)
                {
                    format = GraphicsFormat.R8_UNorm,
                    name = "LPVGrid_" + nameof(PassData.SkyOcclusion),
                }
            );
            passData.Bias = volumeComponent.SkyOcclusionBias.value;
            passData.Amplification = AAAALPVCommon.ComputeEffectiveOcclusionAmplification(volumeComponent.SkyOcclusionAmplification.value);
            passData.GridSize = lpvData.GridSize;
            passData.BlockingPotentialSH = builder.ReadTexture(lpvData.UnpackedGridTextures.BlockingPotentialSH);
            passData.SkyOcclusion = builder.WriteTexture(lpvData.SkyOcclusionTexture);

            _computeShader.GetKernelThreadGroupSizes(KernelIndex, out passData.ThreadGroupSize.x, out passData.ThreadGroupSize.y, out uint _);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            context.cmd.SetComputeFloatParam(_computeShader, ShaderIDs._Bias, data.Bias);
            context.cmd.SetComputeFloatParam(_computeShader, ShaderIDs._Amplification, data.Amplification);
            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._BlockingPotentialSH, data.BlockingPotentialSH);
            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._SkyOcclusion, data.SkyOcclusion);

            var threadGroupSize = (int2) data.ThreadGroupSize;
            int2 threadGroups = AAAAMathUtils.AlignUp(data.GridSize, threadGroupSize) / threadGroupSize;
            context.cmd.DispatchCompute(_computeShader, KernelIndex, threadGroups.x, threadGroups.y, 1);

            context.cmd.SetGlobalTexture(AAAALPVCommon.GlobalShaderIDs._LPVSkyOcclusion, data.SkyOcclusion);
        }

        public class PassData : PassDataBase
        {
            public float Amplification;
            public float Bias;
            public TextureHandle BlockingPotentialSH;
            public int GridSize;
            public TextureHandle SkyOcclusion;
            public uint2 ThreadGroupSize;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static readonly int _BlockingPotentialSH = Shader.PropertyToID(nameof(_BlockingPotentialSH));
            public static readonly int _SkyOcclusion = Shader.PropertyToID(nameof(_SkyOcclusion));
            public static readonly int _Bias = Shader.PropertyToID(nameof(_Bias));
            public static readonly int _Amplification = Shader.PropertyToID(nameof(_Amplification));
        }
    }
}