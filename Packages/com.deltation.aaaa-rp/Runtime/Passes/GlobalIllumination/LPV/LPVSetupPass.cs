using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Lighting;
using DELTation.AAAARP.Volumes;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using static DELTation.AAAARP.Lighting.AAAALightPropagationVolumes;

namespace DELTation.AAAARP.Passes.GlobalIllumination.LPV
{
    public class LPVSetupPass : AAAARenderPass<LPVSetupPass.PassData>
    {
        public LPVSetupPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent) { }

        public override string Name => "LPV.Setup";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAALightPropagationVolumesData lpvData = frameData.GetOrCreate<AAAALightPropagationVolumesData>();
            AAAAShadowsData shadowsData = frameData.Get<AAAAShadowsData>();

            AAAALpvVolumeComponent lpvVolumeComponent = cameraData.VolumeStack.GetComponent<AAAALpvVolumeComponent>();
            lpvData.BlockingPotential = lpvVolumeComponent.BlockingPotential.value;

            passData.GridSize = lpvData.GridSize = (int) lpvVolumeComponent.GridSize.value;
            passData.GridBoundsMin = lpvData.GridBoundsMin = math.float3(-20, -20, -20);
            passData.GridBoundsMax = lpvData.GridBoundsMax = math.float3(20, 20, 20);
            var gridSHDesc = new BufferDesc
            {
                count = passData.GridSize * passData.GridSize * passData.GridSize * 4,
                stride = sizeof(uint),
                target = GraphicsBuffer.Target.Raw,
            };
            const string namePrefix = "LPVGrid_Packed_";
            lpvData.PackedGridBuffers = new AAAALightPropagationVolumesData.GridBufferSet
            {
                SHDesc = gridSHDesc,
                RedSH = renderingData.RenderGraph.CreateBuffer(WithName(gridSHDesc,
                        namePrefix + nameof(AAAALightPropagationVolumesData.GridTextureSet.RedSH)
                    )
                ),
                GreenSH = renderingData.RenderGraph.CreateBuffer(WithName(gridSHDesc,
                        namePrefix + nameof(AAAALightPropagationVolumesData.GridTextureSet.GreenSH)
                    )
                ),
                BlueSH = renderingData.RenderGraph.CreateBuffer(WithName(gridSHDesc,
                        namePrefix + nameof(AAAALightPropagationVolumesData.GridTextureSet.BlueSH)
                    )
                ),
                BlockingPotentialSH = lpvData.BlockingPotential
                    ? renderingData.RenderGraph.CreateBuffer(WithName(gridSHDesc,
                            namePrefix + nameof(AAAALightPropagationVolumesData.GridTextureSet.BlockingPotentialSH)
                        )
                    )
                    : default,
            };

            builder.AllowPassCulling(false);

            const int initialCapacity = 16;
            AllocatorManager.AllocatorHandle allocator = Allocator.Temp;
            lpvData.Lights = new NativeList<RsmLight>(initialCapacity, allocator);
            lpvData.ShadowLightToRSMLightMapping = new NativeHashMap<int, int>(initialCapacity, allocator);
            ref readonly AAAARenderTexturePoolSet rtPoolSet = ref renderingData.RtPoolSet;

            for (int visibleLightIndex = 0; visibleLightIndex < renderingData.CullingResults.visibleLights.Length; visibleLightIndex++)
            {
                if (shadowsData.VisibleToShadowLightMapping.TryGetValue(visibleLightIndex, out int shadowLightIndex))
                {
                    ref readonly VisibleLight visibleLight = ref renderingData.CullingResults.visibleLights.ElementAtRefReadonly(visibleLightIndex);
                    ref readonly AAAAShadowsData.ShadowLight shadowLight = ref shadowsData.ShadowLights.ElementAtRefReadonly(shadowLightIndex);
                    if (shadowLight is { LightType: LightType.Directional, Splits: { Length: > 0 } })
                    {
                        lpvData.Lights.Add(new RsmLight
                            {
                                VisibleLightIndex = visibleLightIndex,
                                ShadowLightIndex = shadowLightIndex,
                                RenderedAllocation = rtPoolSet.AllocateRsmMaps(shadowLight.Resolution),
                                DirectionWS = math.float4(AAAALightingUtils.ExtractDirection(visibleLight.localToWorldMatrix), 0),
                                Color = (Vector4) visibleLight.finalColor,
                            }
                        );
                        lpvData.ShadowLightToRSMLightMapping.Add(shadowLightIndex, lpvData.Lights.Length - 1);
                    }
                }
            }
        }

        private BufferDesc WithName(BufferDesc desc, string name)
        {
            desc.name = name;
            return desc;
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            context.cmd.SetGlobalInt(GlobalShaderIDs._LPVGridSize, data.GridSize);
            context.cmd.SetGlobalVector(GlobalShaderIDs._LPVGridBoundsMin, (Vector3) data.GridBoundsMin);
            context.cmd.SetGlobalVector(GlobalShaderIDs._LPVGridBoundsMax, (Vector3) data.GridBoundsMax);
        }

        public class PassData : PassDataBase
        {
            public float3 GridBoundsMax;
            public float3 GridBoundsMin;
            public int GridSize;
        }
    }
}