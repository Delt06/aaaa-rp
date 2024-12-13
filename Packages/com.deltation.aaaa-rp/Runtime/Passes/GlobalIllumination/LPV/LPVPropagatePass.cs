using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Lighting;
using DELTation.AAAARP.RenderPipelineResources;
using DELTation.AAAARP.Volumes;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.GlobalIllumination.LPV
{
    public class LPVPropagatePass : AAAARenderPass<LPVPropagatePass.PassData>
    {
        private const int KernelIndex = 0;
        private readonly ComputeShader _computeShader;

        public LPVPropagatePass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent)
        {
            AAAALpvRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAALpvRuntimeShaders>();
            _computeShader = shaders.PropagateCS;
        }

        public override string Name => "LPV.Propagate";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();

            AAAALPVVolumeComponent lpvVolumeComponent = cameraData.VolumeStack.GetComponent<AAAALPVVolumeComponent>();
            passData.PassCount = lpvVolumeComponent.PropagationPasses.value;
            if (passData.PassCount == 0)
            {
                return;
            }

            AAAALightPropagationVolumesData lpvData = frameData.Get<AAAALightPropagationVolumesData>();

            passData.Intensity = lpvVolumeComponent.PropagationIntensity.value;
            passData.BlockingPotential = lpvData.BlockingPotential;
            passData.GridSize = lpvData.GridSize;
            passData.OcclusionAmplification = AAAALPVCommon.ComputeEffectiveOcclusionAmplification(lpvVolumeComponent.OcclusionAmplification.value);

            ref readonly AAAALightPropagationVolumesData.GridTextureSet unpackedGridTextures = ref lpvData.UnpackedGridTextures;
            passData.Grid = new GridTextureSet
            {
                RedSH = builder.ReadWriteTexture(unpackedGridTextures.RedSH),
                GreenSH = builder.ReadWriteTexture(unpackedGridTextures.GreenSH),
                BlueSH = builder.ReadWriteTexture(unpackedGridTextures.BlueSH),
            };
            passData.LPVTempGrid = new GridTextureSet
            {
                RedSH = CreateTempGrid(builder, unpackedGridTextures.SHDesc, nameof(PassData.LPVTempGrid) + "_" + nameof(GridTextureSet.RedSH)),
                GreenSH = CreateTempGrid(builder, unpackedGridTextures.SHDesc, nameof(PassData.LPVTempGrid) + "_" + nameof(GridTextureSet.GreenSH)),
                BlueSH = CreateTempGrid(builder, unpackedGridTextures.SHDesc, nameof(PassData.LPVTempGrid) + "_" + nameof(GridTextureSet.BlueSH)),
            };
            passData.BlockingPotentialSH = passData.BlockingPotential ? builder.ReadTexture(lpvData.UnpackedGridTextures.BlockingPotentialSH) : default;

            _computeShader.GetKernelThreadGroupSizes(KernelIndex,
                out passData.ThreadGroupSize.x, out passData.ThreadGroupSize.y, out passData.ThreadGroupSize.z
            );
            return;

            static TextureHandle CreateTempGrid(RenderGraphBuilder builder, TextureDesc desc, string name)
            {
                desc.name = name;
                return builder.CreateTransientTexture(desc);
            }
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            if (data.PassCount == 0)
            {
                return;
            }

            CoreUtils.SetKeyword(context.cmd, _computeShader, AAAALPVCommon.ShaderKeywords.BLOCKING_POTENTIAL, data.BlockingPotential);
            context.cmd.SetComputeFloatParam(_computeShader, ShaderIDs._Intensity, data.Intensity);
            if (data.BlockingPotential)
            {
                context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._BlockingPotentialSH, data.BlockingPotentialSH);
                context.cmd.SetComputeFloatParam(_computeShader, ShaderIDs._OcclusionAmplification, data.OcclusionAmplification);
            }

            for (int i = 0; i < data.PassCount; ++i)
            {
                CoreUtils.SetKeyword(context.cmd, _computeShader, ShaderKeywords.FIRST_STEP, i == 0);
                RenderPass(data, context, data.Grid, data.LPVTempGrid);
                CoreUtils.SetKeyword(context.cmd, _computeShader, ShaderKeywords.FIRST_STEP, false);
                RenderPass(data, context, data.LPVTempGrid, data.Grid);
            }
        }

        private void RenderPass(PassData data, RenderGraphContext context, in GridTextureSet source, in GridTextureSet destination)
        {
            if (math.any(data.ThreadGroupSize == 0))
            {
                return;
            }

            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._SourceRedSH, source.RedSH);
            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._SourceGreenSH, source.GreenSH);
            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._SourceBlueSH, source.BlueSH);
            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._DestinationRedSH, destination.RedSH);
            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._DestinationGreenSH, destination.GreenSH);
            context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._DestinationBlueSH, destination.BlueSH);

            var threadGroupSize = (int3) data.ThreadGroupSize;
            int3 threadGroups = AAAAMathUtils.AlignUp(data.GridSize, threadGroupSize) / threadGroupSize;
            context.cmd.DispatchCompute(_computeShader, KernelIndex, threadGroups.x, threadGroups.y, threadGroups.z);
        }

        public class PassData : PassDataBase
        {
            public bool BlockingPotential;
            public TextureHandle BlockingPotentialSH;
            public GridTextureSet Grid;
            public int GridSize;
            public float Intensity;
            public GridTextureSet LPVTempGrid;
            public float OcclusionAmplification;
            public int PassCount;
            public uint3 ThreadGroupSize;
        }

        public struct GridTextureSet
        {
            public TextureHandle BlueSH;
            public TextureHandle GreenSH;
            public TextureHandle RedSH;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static readonly int _SourceRedSH = Shader.PropertyToID(nameof(_SourceRedSH));
            public static readonly int _SourceGreenSH = Shader.PropertyToID(nameof(_SourceGreenSH));
            public static readonly int _SourceBlueSH = Shader.PropertyToID(nameof(_SourceBlueSH));
            public static readonly int _DestinationRedSH = Shader.PropertyToID(nameof(_DestinationRedSH));
            public static readonly int _DestinationGreenSH = Shader.PropertyToID(nameof(_DestinationGreenSH));
            public static readonly int _DestinationBlueSH = Shader.PropertyToID(nameof(_DestinationBlueSH));
            public static readonly int _BlockingPotentialSH = Shader.PropertyToID(nameof(_BlockingPotentialSH));
            public static readonly int _Intensity = Shader.PropertyToID(nameof(_Intensity));
            public static readonly int _OcclusionAmplification = Shader.PropertyToID(nameof(_OcclusionAmplification));
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderKeywords
        {
            public const string FIRST_STEP = nameof(FIRST_STEP);
        }
    }
}