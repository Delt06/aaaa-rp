using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using DELTation.AAAARP.Volumes;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
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
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAALightPropagationVolumesData lpvData = frameData.GetOrCreate<AAAALightPropagationVolumesData>();

            AAAALpvVolumeComponent lpvVolumeComponent = cameraData.VolumeStack.GetComponent<AAAALpvVolumeComponent>();
            passData.Intensity = IntensityModifier * lpvVolumeComponent.Intensity.value;
            passData.KernelIndex = (int) lpvVolumeComponent.InjectQuality.value;
            passData.GridSize = lpvData.GridSize = (int) lpvVolumeComponent.GridSize.value;
            passData.GridBoundsMin = lpvData.GridBoundsMin = math.float3(-20, -20, -20);
            passData.GridBoundsMax = lpvData.GridBoundsMax = math.float3(20, 20, 20);
            lpvData.GridSHDesc = new TextureDesc
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
            const string namePrefix = "LPV_";
            builder.WriteTexture(passData.GridRedSH = lpvData.GridRedSH =
                renderingData.RenderGraph.CreateTexture(new TextureDesc(lpvData.GridSHDesc)
                    {
                        name = namePrefix + nameof(AAAALightPropagationVolumesData.GridRedSH),
                    }
                )
            );
            builder.WriteTexture(passData.GridGreenSH = lpvData.GridGreenSH =
                renderingData.RenderGraph.CreateTexture(new TextureDesc(lpvData.GridSHDesc)
                    {
                        name = namePrefix + nameof(AAAALightPropagationVolumesData.GridGreenSH),
                    }
                )
            );
            builder.WriteTexture(passData.GridBlueSH = lpvData.GridBlueSH =
                renderingData.RenderGraph.CreateTexture(new TextureDesc(lpvData.GridSHDesc)
                    {
                        name = namePrefix + nameof(AAAALightPropagationVolumesData.GridBlueSH),
                    }
                )
            );
            builder.WriteTexture(passData.GridBlockingPotentialSH = lpvData.GridBlockingPotentialSH =
                renderingData.RenderGraph.CreateTexture(new TextureDesc(lpvData.GridSHDesc)
                    {
                        format = GraphicsFormat.R16G16B16A16_UNorm,
                        name = namePrefix + nameof(AAAALightPropagationVolumesData.GridBlockingPotentialSH),
                    }
                )
            );

            builder.ReadTexture(resourceData.CameraScaledDepthBuffer);
            builder.ReadTexture(resourceData.GBufferNormals);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            context.cmd.SetGlobalInt(ShaderIDs.Global._LPVGridSize, data.GridSize);
            context.cmd.SetGlobalVector(ShaderIDs.Global._LPVGridBoundsMin, (Vector3) data.GridBoundsMin);
            context.cmd.SetGlobalVector(ShaderIDs.Global._LPVGridBoundsMax, (Vector3) data.GridBoundsMax);
            context.cmd.SetGlobalTexture(ShaderIDs.Global._LPVGridRedSH, data.GridRedSH);
            context.cmd.SetGlobalTexture(ShaderIDs.Global._LPVGridGreenSH, data.GridGreenSH);
            context.cmd.SetGlobalTexture(ShaderIDs.Global._LPVGridBlueSH, data.GridBlueSH);

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
            public float3 GridBoundsMax;
            public float3 GridBoundsMin;
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