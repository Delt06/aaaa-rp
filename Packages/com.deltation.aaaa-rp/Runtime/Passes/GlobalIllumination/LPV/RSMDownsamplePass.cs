using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using static DELTation.AAAARP.Lighting.AAAALpvCommon;

namespace DELTation.AAAARP.Passes.GlobalIllumination.LPV
{
    public class RSMDownsamplePass : AAAARenderPass<RSMDownsamplePass.PassData>
    {
        private const int CSKernelIndex = 0;
        public const int DownsampleFactor = 4;
        private readonly ComputeShader _computeShader;

        public RSMDownsamplePass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders shaders) : base(renderPassEvent) =>
            _computeShader = shaders.RsmDownsampleCS;

        public override string Name => "LPV.RSMDownsample";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAALightPropagationVolumesData lpvData = frameData.Get<AAAALightPropagationVolumesData>();

            passData.Batches.Clear();

            ref readonly AAAARenderTexturePoolSet rtPoolSet = ref renderingData.RtPoolSet;

            for (int index = 0; index < lpvData.Lights.Length; index++)
            {
                ref RsmLight rsmLight = ref lpvData.Lights.ElementAtRef(index);
                ref readonly RsmAttachmentAllocation renderedAllocation = ref rsmLight.RenderedAllocation;
                Assert.IsTrue(renderedAllocation.IsValid);

                PassData.Batch batch;

                int destinationSize = renderedAllocation.PositionsMap.Resolution / DownsampleFactor;
                rsmLight.InjectedAllocation = rtPoolSet.AllocateRsmMaps(destinationSize);
                batch.DestinationTextures = PassData.RsmTextureSet.Lookup(rtPoolSet, rsmLight.InjectedAllocation);
                batch.SourceTextures = PassData.RsmTextureSet.Lookup(rtPoolSet, renderedAllocation);
                batch.LightDirectionWS = rsmLight.DirectionWS;
                batch.DestinationSize = new float4(destinationSize, destinationSize, 1.0f / destinationSize, 1.0f / destinationSize);

                passData.Batches.Add(batch);
            }

            _computeShader.GetKernelThreadGroupSizes(CSKernelIndex, out passData.ThreadGroupSizeX, out passData.ThreadGroupSizeY, out uint _);

            builder.AllowPassCulling(false);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            int threadGroupSizeX = (int) data.ThreadGroupSizeX;
            int threadGroupSizeY = (int) data.ThreadGroupSizeY;
            if (threadGroupSizeX == 0 || threadGroupSizeY == 0)
            {
                return;
            }

            foreach (PassData.Batch batch in data.Batches)
            {
                context.cmd.SetComputeVectorParam(_computeShader, ShaderIDs._DestinationSize, batch.DestinationSize);
                context.cmd.SetComputeVectorParam(_computeShader, ShaderIDs._LightDirectionWS, batch.LightDirectionWS);

                context.cmd.SetComputeTextureParam(_computeShader, CSKernelIndex, ShaderIDs._SourcePositionMap, batch.SourceTextures.PositionMap);
                context.cmd.SetComputeTextureParam(_computeShader, CSKernelIndex, ShaderIDs._SourceNormalMap, batch.SourceTextures.NormalMap);
                context.cmd.SetComputeTextureParam(_computeShader, CSKernelIndex, ShaderIDs._SourceFluxMap, batch.SourceTextures.FluxMap);

                context.cmd.SetComputeTextureParam(_computeShader, CSKernelIndex, ShaderIDs._DestinationPositionMap, batch.DestinationTextures.PositionMap);
                context.cmd.SetComputeTextureParam(_computeShader, CSKernelIndex, ShaderIDs._DestinationNormalMap, batch.DestinationTextures.NormalMap);
                context.cmd.SetComputeTextureParam(_computeShader, CSKernelIndex, ShaderIDs._DestinationFluxMap, batch.DestinationTextures.FluxMap);

                context.cmd.DispatchCompute(_computeShader, CSKernelIndex,
                    AAAAMathUtils.AlignUp((int) batch.DestinationSize.x, threadGroupSizeX) / threadGroupSizeX,
                    AAAAMathUtils.AlignUp((int) batch.DestinationSize.y, threadGroupSizeY) / threadGroupSizeY,
                    1
                );
            }
        }

        public class PassData : PassDataBase
        {
            public readonly List<Batch> Batches = new();
            public uint ThreadGroupSizeX;
            public uint ThreadGroupSizeY;

            public struct Batch
            {
                public RsmTextureSet SourceTextures;
                public RsmTextureSet DestinationTextures;
                public float4 LightDirectionWS;
                public float4 DestinationSize;
            }

            public struct RsmTextureSet
            {
                public RenderTexture PositionMap;
                public RenderTexture NormalMap;
                public RenderTexture FluxMap;

                public static RsmTextureSet Lookup(in AAAARenderTexturePoolSet rtPoolSet, in RsmAttachmentAllocation allocation) =>
                    new()
                    {
                        PositionMap = rtPoolSet.RsmPositionMap.LookupRenderTexture(allocation.PositionsMap),
                        NormalMap = rtPoolSet.RsmNormalMap.LookupRenderTexture(allocation.NormalMap),
                        FluxMap = rtPoolSet.RsmFluxMap.LookupRenderTexture(allocation.FluxMap),
                    };
            }
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static readonly int _DestinationSize = Shader.PropertyToID(nameof(_DestinationSize));
            public static readonly int _LightDirectionWS = Shader.PropertyToID(nameof(_LightDirectionWS));
            public static readonly int _SourcePositionMap = Shader.PropertyToID(nameof(_SourcePositionMap));
            public static readonly int _SourceNormalMap = Shader.PropertyToID(nameof(_SourceNormalMap));
            public static readonly int _SourceFluxMap = Shader.PropertyToID(nameof(_SourceFluxMap));
            public static readonly int _DestinationPositionMap = Shader.PropertyToID(nameof(_DestinationPositionMap));
            public static readonly int _DestinationNormalMap = Shader.PropertyToID(nameof(_DestinationNormalMap));
            public static readonly int _DestinationFluxMap = Shader.PropertyToID(nameof(_DestinationFluxMap));
        }
    }
}