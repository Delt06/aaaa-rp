using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Lighting;
using DELTation.AAAARP.Volumes;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using static DELTation.AAAARP.Lighting.AAAALPVCommon;

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

            AAAALPVVolumeComponent lpvVolumeComponent = cameraData.VolumeStack.GetComponent<AAAALPVVolumeComponent>();
            lpvData.BlockingPotential = lpvVolumeComponent.Occlusion.value;

            passData.GridSize = lpvData.GridSize = (int) lpvVolumeComponent.GridSize.value;

            CreateBounds(cameraData, lpvVolumeComponent, out lpvData.GridBoundsMin, out lpvData.GridBoundsMax);
            passData.GridBoundsMin = lpvData.GridBoundsMin;
            passData.GridBoundsMax = lpvData.GridBoundsMax;

            var packedGridSH = new TextureDesc
            {
                width = lpvData.GridSize * lpvData.GridSize,
                height = lpvData.GridSize,
                slices = 1,
                dimension = TextureDimension.Tex2D,
                format = GridFormat,
                clearBuffer = true,
                clearColor = Color.clear,
                msaaSamples = MSAASamples.None,
                useMipMap = false,
            };
            const string namePrefix = "LPVGrid_Packed_";
            lpvData.PackedGridTextures = new AAAALightPropagationVolumesData.GridTextureSet
            {
                SHDesc = packedGridSH,
                RedSH = renderingData.RenderGraph.CreateTexture(new TextureDesc(packedGridSH)
                    {
                        name = namePrefix + nameof(AAAALightPropagationVolumesData.GridTextureSet.RedSH),
                    }
                ),
                GreenSH = renderingData.RenderGraph.CreateTexture(new TextureDesc(packedGridSH)
                    {
                        name = namePrefix + nameof(AAAALightPropagationVolumesData.GridTextureSet.GreenSH),
                    }
                ),
                BlueSH = renderingData.RenderGraph.CreateTexture(new TextureDesc(packedGridSH)
                    {
                        name = namePrefix + nameof(AAAALightPropagationVolumesData.GridTextureSet.BlueSH),
                    }
                ),
                BlockingPotentialSH = lpvData.BlockingPotential
                    ? renderingData.RenderGraph.CreateTexture(new TextureDesc(packedGridSH)
                        {
                            name = namePrefix + nameof(AAAALightPropagationVolumesData.GridTextureSet.BlockingPotentialSH),
                            format = GridBlockingPotentialFormat,
                        }
                    )
                    : default,
            };

            lpvData.UnpackedGridTextures.SHDesc = new TextureDesc
            {
                width = lpvData.GridSize,
                height = lpvData.GridSize,
                slices = lpvData.GridSize,
                dimension = TextureDimension.Tex3D,
                format = GridFormat,
                enableRandomWrite = true,
                filterMode = FilterMode.Trilinear,
                msaaSamples = MSAASamples.None,
                useMipMap = false,
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