using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using DELTation.AAAARP.Volumes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.GlobalIllumination.LPV
{
    public class LPVInjectPass : AAAARenderPass<LPVInjectPass.PassData>
    {
        private const float IntensityModifier = 0.5f;
        private readonly ComputeShader _computeShader;

        public LPVInjectPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders shaders) : base(renderPassEvent) =>
            _computeShader = shaders.LpvInjectCS;

        public override string Name => "LPV.Inject";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAALightPropagationVolumesData lpvData = frameData.GetOrCreate<AAAALightPropagationVolumesData>();

            AAAALpvVolumeComponent lpvVolumeComponent = cameraData.VolumeStack.GetComponent<AAAALpvVolumeComponent>();
            passData.Intensity = IntensityModifier * lpvVolumeComponent.Intensity.value;
            passData.KernelIndex = (int) lpvVolumeComponent.InjectQuality.value;
            passData.GridSize = lpvData.GridSize;
            builder.ReadWriteTexture(passData.GridRedSH = lpvData.GridRedSH);
            builder.ReadWriteTexture(passData.GridGreenSH = lpvData.GridGreenSH);
            builder.ReadWriteTexture(passData.GridBlueSH = lpvData.GridBlueSH);
            builder.ReadWriteTexture(passData.GridBlockingPotentialSH = lpvData.GridBlockingPotentialSH);

            builder.ReadTexture(resourceData.CameraScaledDepthBuffer);
            builder.ReadTexture(resourceData.GBufferNormals);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            int kernelIndex = data.KernelIndex;
            context.cmd.SetComputeTextureParam(_computeShader, kernelIndex, ShaderIDs._GridRedUAV, data.GridRedSH);
            context.cmd.SetComputeTextureParam(_computeShader, kernelIndex, ShaderIDs._GridGreenUAV, data.GridGreenSH);
            context.cmd.SetComputeTextureParam(_computeShader, kernelIndex, ShaderIDs._GridBlueUAV, data.GridBlueSH);
            context.cmd.SetComputeTextureParam(_computeShader, kernelIndex, ShaderIDs._GridBlockingPotentialUAV, data.GridBlockingPotentialSH);
            context.cmd.SetComputeFloatParam(_computeShader, ShaderIDs._Intensity, data.Intensity);
            context.cmd.DispatchCompute(_computeShader, kernelIndex, data.GridSize, data.GridSize, data.GridSize);
        }

        public class PassData : PassDataBase
        {
            public TextureHandle GridBlockingPotentialSH;
            public TextureHandle GridBlueSH;
            public TextureHandle GridGreenSH;
            public TextureHandle GridRedSH;
            public int GridSize;
            public float Intensity;
            public int KernelIndex;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static readonly int _GridRedUAV = Shader.PropertyToID(nameof(_GridRedUAV));
            public static readonly int _GridGreenUAV = Shader.PropertyToID(nameof(_GridGreenUAV));
            public static readonly int _GridBlueUAV = Shader.PropertyToID(nameof(_GridBlueUAV));
            public static readonly int _GridBlockingPotentialUAV = Shader.PropertyToID(nameof(_GridBlockingPotentialUAV));
            public static readonly int _Intensity = Shader.PropertyToID(nameof(_Intensity));
        }
    }
}