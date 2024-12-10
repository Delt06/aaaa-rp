using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using static DELTation.AAAARP.Lighting.AAAALightPropagationVolumes;

namespace DELTation.AAAARP.Passes.GlobalIllumination.LPV
{
    public class LPVInjectPass : AAAARenderPass<LPVInjectPass.PassData>
    {
        private const int KernelIndex = 0;
        private readonly ComputeShader _computeShader;

        public LPVInjectPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders shaders) : base(renderPassEvent) =>
            _computeShader = shaders.LpvInjectCS;

        public override string Name => "LPV.Inject";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            AAAALightPropagationVolumesData lpvData = frameData.GetOrCreate<AAAALightPropagationVolumesData>();

            ref readonly AAAALightPropagationVolumesData.GridBufferSet gridBuffers = ref lpvData.PackedGridBuffers;
            builder.WriteBuffer(passData.GridRedSH = gridBuffers.RedSH);
            builder.WriteBuffer(passData.GridGreenSH = gridBuffers.GreenSH);
            builder.WriteBuffer(passData.GridBlueSH = gridBuffers.BlueSH);
            builder.WriteBuffer(passData.GridBlockingPotentialSH = gridBuffers.BlockingPotentialSH);

            passData.Batches.Clear();

            _computeShader.GetKernelThreadGroupSizes(KernelIndex, out uint threadGroupSizeX, out uint threadGroupSizeY, out uint _);

            ref readonly AAAARenderTexturePoolSet rtPoolSet = ref renderingData.RtPoolSet;

            for (int index = 0; index < lpvData.Lights.Length; index++)
            {
                ref readonly RsmLight rsmLight = ref lpvData.Lights.ElementAtRefReadonly(index);
                ref readonly RsmAttachmentAllocation rsmAllocation = ref rsmLight.InjectedAllocation;
                int resolution = rsmAllocation.PositionsMap.Resolution;
                passData.Batches.Add(new PassData.Batch
                    {
                        RsmPositionMap = rtPoolSet.RsmPositionMap.LookupRenderTexture(rsmAllocation.PositionsMap),
                        RsmNormalMap = rtPoolSet.RsmNormalMap.LookupRenderTexture(rsmAllocation.NormalMap),
                        RsmFluxMap = rtPoolSet.RsmFluxMap.LookupRenderTexture(rsmAllocation.FluxMap),
                        LightDirectionWS = rsmLight.DirectionWS,
                        LightColor = rsmLight.Color,
                        RsmResolution = math.float4(resolution, resolution, math.float2(math.rcp(resolution))),
                        ThreadGroupsX = AAAAMathUtils.AlignUp(resolution, (int) threadGroupSizeX) / (int) threadGroupSizeX,
                        ThreadGroupsY = AAAAMathUtils.AlignUp(resolution, (int) threadGroupSizeY) / (int) threadGroupSizeY,
                    }
                );
            }

            builder.ReadTexture(resourceData.CameraScaledDepthBuffer);
            builder.ReadTexture(resourceData.GBufferNormals);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            context.cmd.SetComputeBufferParam(_computeShader, KernelIndex, ShaderIDs._GridRedUAV, data.GridRedSH);
            context.cmd.SetComputeBufferParam(_computeShader, KernelIndex, ShaderIDs._GridGreenUAV, data.GridGreenSH);
            context.cmd.SetComputeBufferParam(_computeShader, KernelIndex, ShaderIDs._GridBlueUAV, data.GridBlueSH);
            context.cmd.SetComputeBufferParam(_computeShader, KernelIndex, ShaderIDs._GridBlockingPotentialUAV, data.GridBlockingPotentialSH);

            foreach (PassData.Batch batch in data.Batches)
            {
                context.cmd.SetComputeVectorParam(_computeShader, ShaderIDs._LightDirectionWS, batch.LightDirectionWS);
                context.cmd.SetComputeVectorParam(_computeShader, ShaderIDs._LightColor, batch.LightColor);
                context.cmd.SetComputeVectorParam(_computeShader, ShaderIDs._RSMResolution, batch.RsmResolution);
                context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._RSMPositionMap, batch.RsmPositionMap);
                context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._RSMNormalMap, batch.RsmNormalMap);
                context.cmd.SetComputeTextureParam(_computeShader, KernelIndex, ShaderIDs._RSMFluxMap, batch.RsmFluxMap);
                context.cmd.DispatchCompute(_computeShader, KernelIndex, batch.ThreadGroupsX, batch.ThreadGroupsY, 1);
            }
        }

        public class PassData : PassDataBase
        {
            public readonly List<Batch> Batches = new();
            public BufferHandle GridBlockingPotentialSH;
            public BufferHandle GridBlueSH;
            public BufferHandle GridGreenSH;
            public BufferHandle GridRedSH;

            public struct Batch
            {
                public RenderTargetIdentifier RsmPositionMap;
                public RenderTargetIdentifier RsmNormalMap;
                public RenderTargetIdentifier RsmFluxMap;
                public float4 LightDirectionWS;
                public float4 RsmResolution;
                public int ThreadGroupsX;
                public int ThreadGroupsY;
                public float4 LightColor;
            }
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static readonly int _GridRedUAV = Shader.PropertyToID(nameof(_GridRedUAV));
            public static readonly int _GridGreenUAV = Shader.PropertyToID(nameof(_GridGreenUAV));
            public static readonly int _GridBlueUAV = Shader.PropertyToID(nameof(_GridBlueUAV));
            public static readonly int _GridBlockingPotentialUAV = Shader.PropertyToID(nameof(_GridBlockingPotentialUAV));
            public static readonly int _LightDirectionWS = Shader.PropertyToID(nameof(_LightDirectionWS));
            public static readonly int _LightColor = Shader.PropertyToID(nameof(_LightColor));
            public static readonly int _RSMResolution = Shader.PropertyToID(nameof(_RSMResolution));
            public static readonly int _RSMPositionMap = Shader.PropertyToID(nameof(_RSMPositionMap));
            public static readonly int _RSMNormalMap = Shader.PropertyToID(nameof(_RSMNormalMap));
            public static readonly int _RSMFluxMap = Shader.PropertyToID(nameof(_RSMFluxMap));
        }
    }
}