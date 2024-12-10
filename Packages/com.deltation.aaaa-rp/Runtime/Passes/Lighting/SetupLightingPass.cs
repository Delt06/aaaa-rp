using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.Data;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Lighting;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.Lighting
{
    public sealed class SetupLightingPass : AAAARenderPass<SetupLightingPass.PassData>
    {
        private const int NoShadowMapIndex = -1;
        private readonly GlobalKeywords _globalKeywords;

        public SetupLightingPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent) => _globalKeywords = GlobalKeywords.Create();

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAAImageBasedLightingData imageBasedLightingData = frameData.Get<AAAAImageBasedLightingData>();
            AAAAShadowsData shadowsData = frameData.Get<AAAAShadowsData>();

            AAAALightingData lightingData = frameData.Get<AAAALightingData>();
            lightingData.Init(renderingData.RenderGraph, renderingData.PipelineAsset.LightingSettings);

            var punctualLights = new NativeList<AAAAPunctualLightData>(renderingData.CullingResults.visibleLights.Length, Allocator.Temp);
            var shadowLightSlices = new NativeList<AAAAShadowLightSlice>(shadowsData.ShadowLights.Length * 4, Allocator.Temp);
            ref AAAALightingConstantBuffer lightingConstantBuffer = ref lightingData.LightingConstantBuffer;
            FillLightsData(renderingData, shadowsData, ref lightingConstantBuffer, punctualLights, shadowLightSlices);

            passData.PunctualLights = punctualLights.AsArray();
            passData.PunctualLightsBuffer = builder.WriteBuffer(lightingData.PunctualLightsBuffer);

            passData.ShadowLightSlices = shadowLightSlices.AsArray();
            passData.ShadowLightSlicesBuffer = builder.WriteBuffer(shadowsData.ShadowLightSlicesBuffer);

            lightingData.AmbientIntensity = RenderSettings.ambientIntensity;

            passData.LightingData = lightingData;

            passData.DiffuseIrradianceCubemap = builder.ReadTexture(imageBasedLightingData.DiffuseIrradiance);
            passData.BRDFLut = builder.ReadTexture(imageBasedLightingData.BRDFLut);
            passData.PreFilteredEnvironmentMap = builder.ReadTexture(imageBasedLightingData.PreFilteredEnvironmentMap);
            passData.PreFilteredEnvironmentMapMaxLOD = imageBasedLightingData.PreFilteredEnvironmentMapDesc.mipCount - 1;
            passData.AmbientOcclusionTechnique = cameraData.AmbientOcclusionTechnique;
            passData.RealtimeGITecninque = cameraData.RealtimeGITechnique;
            passData.XeGTAOBentNormals = renderingData.PipelineAsset.LightingSettings.GTAOSettings.BentNormals;
            passData.XeGTAODirectLightingMicroshadows = renderingData.PipelineAsset.LightingSettings.GTAOSettings.DirectLightingMicroshadows;

            builder.AllowPassCulling(false);
        }

        private static unsafe void FillLightsData(AAAARenderingData renderingData, AAAAShadowsData shadowsData,
            ref AAAALightingConstantBuffer lightingConstantBuffer,
            NativeList<AAAAPunctualLightData> punctualLights, NativeList<AAAAShadowLightSlice> shadowLightSlices)
        {
            int maxPunctualLights = renderingData.PipelineAsset.LightingSettings.MaxPunctualLights;
            lightingConstantBuffer.DirectionalLightCount = 0;

            fixed (AAAALightingConstantBuffer* pConstantBuffer = &lightingConstantBuffer)
            {
                for (int visibleLightIndex = 0; visibleLightIndex < renderingData.CullingResults.visibleLights.Length; visibleLightIndex++)
                {
                    ref readonly VisibleLight visibleLight = ref renderingData.CullingResults.visibleLights.ElementAtRefReadonly(visibleLightIndex);

                    if (visibleLight.lightType is LightType.Point or LightType.Spot &&
                        punctualLights.Length < maxPunctualLights)
                    {
                        AAAAShadowsData.ShadowLight shadowLight = default;
                        int shadowSplitIndex = -1;

                        if (shadowsData.VisibleToShadowLightMapping.TryGetValue(visibleLightIndex, out int shadowLightIndex))
                        {
                            shadowLight = shadowsData.ShadowLights.ElementAtRef(shadowLightIndex);
                            shadowSplitIndex = shadowLightSlices.Length;

                            foreach (AAAAShadowsData.ShadowLightSplit shadowLightSplit in shadowLight.Splits)
                            {
                                shadowLightSlices.Add(BuildShadowLightSlice(renderingData, shadowLightSplit));
                            }
                        }

                        AAAAPunctualLightData punctualLightData = ExtractPunctualLightData(visibleLight, shadowLight, shadowSplitIndex);
                        punctualLights.Add(punctualLightData);
                    }

                    if (visibleLight.lightType == LightType.Directional &&
                        lightingConstantBuffer.DirectionalLightCount < AAAALightingConstantBuffer.MaxDirectionalLights)
                    {
                        int index = (int) lightingConstantBuffer.DirectionalLightCount++;
                        var shadowSliceRangeFadeParams = new float4(0, 0, 0, 0);
                        bool isSoftShadow = false;
                        float shadowStrength = 1.0f;

                        if (shadowsData.VisibleToShadowLightMapping.TryGetValue(visibleLightIndex, out int shadowLightIndex))
                        {
                            ref readonly AAAAShadowsData.ShadowLight shadowLight = ref shadowsData.ShadowLights.ElementAtRef(shadowLightIndex);

                            shadowSliceRangeFadeParams.x = shadowLightSlices.Length;
                            shadowSliceRangeFadeParams.y = shadowLight.Splits.Length;
                            shadowSliceRangeFadeParams.zw = shadowLight.FadeParams;
                            isSoftShadow = shadowLight.IsSoftShadow;
                            shadowStrength = shadowLight.ShadowStrength;

                            foreach (AAAAShadowsData.ShadowLightSplit shadowLightSplit in shadowLight.Splits)
                            {
                                shadowLightSlices.Add(BuildShadowLightSlice(renderingData, shadowLightSplit));
                            }
                        }

                        UnsafeUtility.ArrayElementAsRef<float4>(pConstantBuffer->DirectionalLightColors, index) =
                            (Vector4) visibleLight.finalColor;
                        UnsafeUtility.ArrayElementAsRef<float4>(pConstantBuffer->DirectionalLightDirections, index) =
                            math.float4(AAAALightingUtils.ExtractDirection(visibleLight.localToWorldMatrix), 0.0f);
                        UnsafeUtility.ArrayElementAsRef<float4>(pConstantBuffer->DirectionalLightShadowSliceRanges_ShadowFadeParams, index) =
                            shadowSliceRangeFadeParams;
                        UnsafeUtility.ArrayElementAsRef<float4>(pConstantBuffer->DirectionalLightShadowParams, index) =
                            PackShadowParams(isSoftShadow, shadowStrength);
                    }
                }

                if (lightingConstantBuffer.DirectionalLightCount == 0)
                {
                    UnsafeUtility.ArrayElementAsRef<float4>(pConstantBuffer->DirectionalLightColors, 0) = float4.zero;
                    UnsafeUtility.ArrayElementAsRef<float4>(pConstantBuffer->DirectionalLightDirections, 0) = float4.zero;
                    UnsafeUtility.ArrayElementAsRef<float4>(pConstantBuffer->DirectionalLightShadowSliceRanges_ShadowFadeParams, 0) = float4.zero;
                    UnsafeUtility.ArrayElementAsRef<float4>(pConstantBuffer->DirectionalLightShadowParams, 0) = float4.zero;
                }
            }

            lightingConstantBuffer.PunctualLightCount = (uint) punctualLights.Length;
        }

        private static AAAAShadowLightSlice BuildShadowLightSlice(AAAARenderingData renderingData, in AAAAShadowsData.ShadowLightSplit shadowLightSplit)
        {
            float2 resolution = shadowLightSplit.CullingView.PixelSize;
            float4 boundingSphere = shadowLightSplit.CullingView.BoundingSphereWS;
            AAAARenderTexturePool shadowMapPool = renderingData.RtPoolSet.ShadowMap;
            var shadowLightSlice = new AAAAShadowLightSlice
            {
                BoundingSphere = math.float4(boundingSphere.xyz, boundingSphere.w * boundingSphere.w),
                AtlasSize = math.float4(1.0f / resolution, resolution),
                WorldToShadowCoords = AAAAShadowUtils.GetWorldToShadowCoordsMatrix(shadowLightSplit.CullingView.ViewProjectionMatrix),
                BindlessShadowMapIndex = shadowMapPool.GetBindlessSRVIndexOrDefault(shadowLightSplit.ShadowMapAllocation, NoShadowMapIndex),
            };

            return shadowLightSlice;
        }

        private static AAAAPunctualLightData ExtractPunctualLightData(in VisibleLight visibleLight, in AAAAShadowsData.ShadowLight shadowLight, int sliceIndex)
        {
            Matrix4x4 lightLocalToWorld = visibleLight.localToWorldMatrix;
            var punctualLightData = new AAAAPunctualLightData();

            punctualLightData.Color_Radius.xyz = ((float4) (Vector4) visibleLight.finalColor).xyz;
            punctualLightData.Color_Radius.w = visibleLight.range;

            punctualLightData.PositionWS.xyz = lightLocalToWorld.GetPosition();
            if (visibleLight.lightType == LightType.Spot)
            {
                punctualLightData.SpotDirection_Angle.xyz = AAAALightingUtils.ExtractDirection(lightLocalToWorld);
                punctualLightData.SpotDirection_Angle.w = Mathf.Deg2Rad * visibleLight.spotAngle * 0.5f;
            }
            else
            {
                punctualLightData.SpotDirection_Angle = default;
            }

            AAAAPunctualLightUtils.GetPunctualLightDistanceAttenuation(visibleLight,
                out punctualLightData.Attenuations.x
            );
            float innerSpotAngle = visibleLight.spotAngle * 0.6f;
            AAAAPunctualLightUtils.GetPunctualLightSpotAngleAttenuation(visibleLight, innerSpotAngle, out punctualLightData.Attenuations.y,
                out punctualLightData.Attenuations.z
            );

            if (sliceIndex != -1)
            {
                punctualLightData.ShadowSliceIndex_ShadowFadeParams.x = sliceIndex;
                punctualLightData.ShadowSliceIndex_ShadowFadeParams.zw = shadowLight.FadeParams;
                punctualLightData.ShadowParams = PackShadowParams(shadowLight.IsSoftShadow, shadowLight.ShadowStrength);
            }
            else
            {
                punctualLightData.ShadowSliceIndex_ShadowFadeParams.x = -1;
            }

            return punctualLightData;
        }

        private static float4 PackShadowParams(bool isSoftShadow, float shadowStrength) => math.float4(isSoftShadow ? 1 : 0, shadowStrength, 0, 0);

        protected override void Render(PassData data, RenderGraphContext context)
        {
            ConstantBuffer.PushGlobal(context.cmd, data.LightingData.LightingConstantBuffer, ShaderPropertyID.LightingConstantBuffer);

            context.cmd.SetBufferData(data.PunctualLightsBuffer, data.PunctualLights);
            context.cmd.SetGlobalBuffer(ShaderPropertyID._PunctualLights, data.PunctualLightsBuffer);

            context.cmd.SetBufferData(data.ShadowLightSlicesBuffer, data.ShadowLightSlices);
            context.cmd.SetGlobalBuffer(ShaderPropertyID._ShadowLightSlices, data.ShadowLightSlicesBuffer);

            context.cmd.SetGlobalTexture(ShaderPropertyID.aaaa_DiffuseIrradianceCubemap, data.DiffuseIrradianceCubemap);
            context.cmd.SetGlobalFloat(ShaderPropertyID.aaaa_AmbientIntensity, data.LightingData.AmbientIntensity);
            context.cmd.SetGlobalTexture(ShaderPropertyID.aaaa_BRDFLut, data.BRDFLut);
            context.cmd.SetGlobalTexture(ShaderPropertyID.aaaa_PreFilteredEnvironmentMap, data.PreFilteredEnvironmentMap);
            context.cmd.SetGlobalFloat(ShaderPropertyID.aaaa_PreFilteredEnvironmentMap_MaxLOD, data.PreFilteredEnvironmentMapMaxLOD);

            var shCoefficients = new SHCoefficients(RenderSettings.ambientProbe);
            context.cmd.SetGlobalVector(ShaderPropertyID.unity_SHAr, shCoefficients.SHAr);
            context.cmd.SetGlobalVector(ShaderPropertyID.unity_SHAg, shCoefficients.SHAg);
            context.cmd.SetGlobalVector(ShaderPropertyID.unity_SHAb, shCoefficients.SHAb);
            context.cmd.SetGlobalVector(ShaderPropertyID.unity_SHBr, shCoefficients.SHBr);
            context.cmd.SetGlobalVector(ShaderPropertyID.unity_SHBg, shCoefficients.SHBg);
            context.cmd.SetGlobalVector(ShaderPropertyID.unity_SHBb, shCoefficients.SHBb);
            context.cmd.SetGlobalVector(ShaderPropertyID.unity_SHC, shCoefficients.SHC);

            context.cmd.SetKeyword(_globalKeywords.GTAO, data.AmbientOcclusionTechnique == AAAAAmbientOcclusionTechnique.XeGTAO && !data.XeGTAOBentNormals);
            context.cmd.SetKeyword(_globalKeywords.GTAOBentNormals,
                data.AmbientOcclusionTechnique == AAAAAmbientOcclusionTechnique.XeGTAO && data.XeGTAOBentNormals
            );
            context.cmd.SetKeyword(_globalKeywords.DirectLightingAOMicroshadows,
                data.AmbientOcclusionTechnique == AAAAAmbientOcclusionTechnique.XeGTAO && data.XeGTAODirectLightingMicroshadows
            );

            context.cmd.SetKeyword(_globalKeywords.LPV, data.RealtimeGITecninque == AAAARealtimeGITechnique.LightPropagationVolumes);
        }

        private struct GlobalKeywords
        {
            public GlobalKeyword DirectLightingAOMicroshadows;
            public GlobalKeyword GTAO;
            public GlobalKeyword GTAOBentNormals;
            public GlobalKeyword LPV;

            public static GlobalKeywords Create() =>
                new()
                {
                    GTAO = GlobalKeyword.Create("AAAA_GTAO"),
                    GTAOBentNormals = GlobalKeyword.Create("AAAA_GTAO_BENT_NORMALS"),
                    DirectLightingAOMicroshadows = GlobalKeyword.Create("AAAA_DIRECT_LIGHTING_AO_MICROSHADOWS"),
                    LPV = GlobalKeyword.Create("AAAA_LPV"),
                };
        }

        public class PassData : PassDataBase
        {
            public AAAAAmbientOcclusionTechnique AmbientOcclusionTechnique;
            public TextureHandle BRDFLut;
            public TextureHandle DiffuseIrradianceCubemap;
            public AAAALightingData LightingData;
            public TextureHandle PreFilteredEnvironmentMap;
            public float PreFilteredEnvironmentMapMaxLOD;
            public NativeArray<AAAAPunctualLightData> PunctualLights;
            public BufferHandle PunctualLightsBuffer;
            public AAAARealtimeGITechnique RealtimeGITecninque;
            public NativeArray<AAAAShadowLightSlice> ShadowLightSlices;
            public BufferHandle ShadowLightSlicesBuffer;
            public bool XeGTAOBentNormals;
            public bool XeGTAODirectLightingMicroshadows;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderPropertyID
        {
            public static readonly int LightingConstantBuffer = Shader.PropertyToID(nameof(AAAALightingConstantBuffer));
            public static readonly int _PunctualLights = Shader.PropertyToID(nameof(_PunctualLights));
            public static readonly int _ShadowLightSlices = Shader.PropertyToID(nameof(_ShadowLightSlices));

            public static readonly int aaaa_DiffuseIrradianceCubemap = Shader.PropertyToID(nameof(aaaa_DiffuseIrradianceCubemap));
            public static readonly int aaaa_AmbientIntensity = Shader.PropertyToID(nameof(aaaa_AmbientIntensity));
            public static readonly int aaaa_BRDFLut = Shader.PropertyToID(nameof(aaaa_BRDFLut));
            public static readonly int aaaa_PreFilteredEnvironmentMap = Shader.PropertyToID(nameof(aaaa_PreFilteredEnvironmentMap));
            public static readonly int aaaa_PreFilteredEnvironmentMap_MaxLOD = Shader.PropertyToID(nameof(aaaa_PreFilteredEnvironmentMap_MaxLOD));

            public static readonly int unity_SHAr = Shader.PropertyToID(nameof(unity_SHAr));
            public static readonly int unity_SHAg = Shader.PropertyToID(nameof(unity_SHAg));
            public static readonly int unity_SHAb = Shader.PropertyToID(nameof(unity_SHAb));
            public static readonly int unity_SHBr = Shader.PropertyToID(nameof(unity_SHBr));
            public static readonly int unity_SHBg = Shader.PropertyToID(nameof(unity_SHBg));
            public static readonly int unity_SHBb = Shader.PropertyToID(nameof(unity_SHBb));
            public static readonly int unity_SHC = Shader.PropertyToID(nameof(unity_SHC));
        }
    }
}