using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Lighting;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes
{
    public sealed class SetupLightingPass : AAAARenderPass<SetupLightingPass.PassData>
    {
        public SetupLightingPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent) { }

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAAImageBasedLightingData imageBasedLightingData = frameData.Get<AAAAImageBasedLightingData>();
            AAAALightingData lightingData = frameData.Get<AAAALightingData>();
            lightingData.Init(renderingData.RenderGraph, renderingData.PipelineAsset.LightingSettings);

            ref AAAALightingConstantBuffer lightingConstantBuffer = ref lightingData.LightingConstantBuffer;
            var punctualLights = new NativeList<AAAAPunctualLightData>(renderingData.CullingResults.visibleLights.Length, Allocator.Temp);
            FillLightsData(renderingData, ref lightingConstantBuffer, punctualLights);

            passData.PunctualLightsBuffer = builder.WriteBuffer(lightingData.PunctualLightsBuffer);
            passData.PunctualLights = punctualLights.AsArray();

            lightingData.AmbientIntensity = RenderSettings.ambientIntensity;

            passData.LightingData = lightingData;

            passData.DiffuseIrradianceCubemap = builder.ReadTexture(imageBasedLightingData.DiffuseIrradiance);
            passData.BRDFLut = builder.ReadTexture(imageBasedLightingData.BRDFLut);
            passData.PreFilteredEnvironmentMap = builder.ReadTexture(imageBasedLightingData.PreFilteredEnvironmentMap);
            passData.PreFilteredEnvironmentMapMaxLOD = imageBasedLightingData.PreFilteredEnvironmentMapDesc.mipCount - 1;

            builder.AllowPassCulling(false);
        }

        private static unsafe void FillLightsData(AAAARenderingData renderingData, ref AAAALightingConstantBuffer lightingConstantBuffer,
            NativeList<AAAAPunctualLightData> punctualLights)
        {
            int maxPunctualLights = renderingData.PipelineAsset.LightingSettings.MaxPunctualLights;
            lightingConstantBuffer.DirectionalLightCount = 0;

            fixed (float* pDirectionalLightColorsFloat = lightingConstantBuffer.DirectionalLightColors)
            {
                var pDirectionalLightColors = (Vector4*) pDirectionalLightColorsFloat;

                fixed (float* pDirectionalLightDirectionsFloat = lightingConstantBuffer.DirectionalLightDirections)
                {
                    var pDirectionalLightDirections = (Vector4*) pDirectionalLightDirectionsFloat;

                    foreach (VisibleLight visibleLight in renderingData.CullingResults.visibleLights)
                    {

                        if (visibleLight.lightType is LightType.Point or LightType.Spot &&
                            punctualLights.Length < maxPunctualLights)
                        {
                            AAAAPunctualLightData punctualLightData = ExtractPunctualLightData(visibleLight);
                            punctualLights.Add(punctualLightData);
                        }

                        if (visibleLight.lightType == LightType.Directional &&
                            lightingConstantBuffer.DirectionalLightCount < AAAALightingConstantBuffer.MaxDirectionalLights)
                        {
                            uint index = lightingConstantBuffer.DirectionalLightCount++;
                            pDirectionalLightColors[index] = visibleLight.finalColor;
                            pDirectionalLightDirections[index] = (visibleLight.localToWorldMatrix * Vector3.back).normalized;
                        }
                    }

                    if (lightingConstantBuffer.DirectionalLightCount == 0)
                    {
                        pDirectionalLightColors[0] = Vector4.zero;
                        pDirectionalLightDirections[0] = Vector4.zero;
                    }
                }
            }

            lightingConstantBuffer.PunctualLightCount = (uint) punctualLights.Length;
        }

        private static AAAAPunctualLightData ExtractPunctualLightData(VisibleLight visibleLight)
        {
            Matrix4x4 lightLocalToWorld = visibleLight.localToWorldMatrix;
            var punctualLightData = new AAAAPunctualLightData();

            punctualLightData.Color_Radius.xyz = ((float4) (Vector4) visibleLight.finalColor).xyz;
            punctualLightData.Color_Radius.w = visibleLight.range;

            punctualLightData.PositionWS.xyz = lightLocalToWorld.GetPosition();
            punctualLightData.SpotDirection.xyz = visibleLight.lightType == LightType.Spot ? -((float4) lightLocalToWorld.GetColumn(2)).xyz : default;

            AAAAPunctualLightUtils.GetPunctualLightDistanceAttenuation(visibleLight, out punctualLightData.Attenuations.x);
            AAAAPunctualLightUtils.GetPunctualLightSpotAngleAttenuation(visibleLight, null, out punctualLightData.Attenuations.y, out punctualLightData.Attenuations.z);

            return punctualLightData;
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            ConstantBuffer.PushGlobal(context.cmd, data.LightingData.LightingConstantBuffer, ShaderPropertyID.LightingConstantBuffer);

            context.cmd.SetBufferData(data.PunctualLightsBuffer, data.PunctualLights);
            context.cmd.SetGlobalBuffer(ShaderPropertyID._PunctualLights, data.PunctualLightsBuffer);

            context.cmd.SetGlobalTexture(ShaderPropertyID.aaaa_DiffuseIrradianceCubemap, data.DiffuseIrradianceCubemap);
            context.cmd.SetGlobalFloat(ShaderPropertyID.aaaa_AmbientIntensity, data.LightingData.AmbientIntensity);
            context.cmd.SetGlobalTexture(ShaderPropertyID.aaaa_BRDFLut, data.BRDFLut);
            context.cmd.SetGlobalTexture(ShaderPropertyID.aaaa_PreFilteredEnvironmentMap, data.PreFilteredEnvironmentMap);
            context.cmd.SetGlobalFloat(ShaderPropertyID.aaaa_PreFilteredEnvironmentMap_MaxLOD, data.PreFilteredEnvironmentMapMaxLOD);

            var shCoefficients = new SHCoefficients(RenderSettings.ambientProbe);
            context.cmd.SetGlobalVector(ShaderPropertyID.aaaa_SHAr, shCoefficients.SHAr);
            context.cmd.SetGlobalVector(ShaderPropertyID.aaaa_SHAg, shCoefficients.SHAg);
            context.cmd.SetGlobalVector(ShaderPropertyID.aaaa_SHAb, shCoefficients.SHAb);
            context.cmd.SetGlobalVector(ShaderPropertyID.aaaa_SHBr, shCoefficients.SHBr);
            context.cmd.SetGlobalVector(ShaderPropertyID.aaaa_SHBg, shCoefficients.SHBg);
            context.cmd.SetGlobalVector(ShaderPropertyID.aaaa_SHBb, shCoefficients.SHBb);
            context.cmd.SetGlobalVector(ShaderPropertyID.aaaa_SHC, shCoefficients.SHC);
        }

        public class PassData : PassDataBase
        {
            public TextureHandle BRDFLut;
            public TextureHandle DiffuseIrradianceCubemap;
            public AAAALightingData LightingData;
            public TextureHandle PreFilteredEnvironmentMap;
            public float PreFilteredEnvironmentMapMaxLOD;
            public NativeArray<AAAAPunctualLightData> PunctualLights;
            public BufferHandle PunctualLightsBuffer;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderPropertyID
        {
            public static readonly int LightingConstantBuffer = Shader.PropertyToID(nameof(AAAALightingConstantBuffer));
            public static readonly int _PunctualLights = Shader.PropertyToID(nameof(_PunctualLights));

            public static readonly int aaaa_DiffuseIrradianceCubemap = Shader.PropertyToID(nameof(aaaa_DiffuseIrradianceCubemap));
            public static readonly int aaaa_AmbientIntensity = Shader.PropertyToID(nameof(aaaa_AmbientIntensity));
            public static readonly int aaaa_BRDFLut = Shader.PropertyToID(nameof(aaaa_BRDFLut));
            public static readonly int aaaa_PreFilteredEnvironmentMap = Shader.PropertyToID(nameof(aaaa_PreFilteredEnvironmentMap));
            public static readonly int aaaa_PreFilteredEnvironmentMap_MaxLOD = Shader.PropertyToID(nameof(aaaa_PreFilteredEnvironmentMap_MaxLOD));

            public static readonly int aaaa_SHAr = Shader.PropertyToID(nameof(aaaa_SHAr));
            public static readonly int aaaa_SHAg = Shader.PropertyToID(nameof(aaaa_SHAg));
            public static readonly int aaaa_SHAb = Shader.PropertyToID(nameof(aaaa_SHAb));
            public static readonly int aaaa_SHBr = Shader.PropertyToID(nameof(aaaa_SHBr));
            public static readonly int aaaa_SHBg = Shader.PropertyToID(nameof(aaaa_SHBg));
            public static readonly int aaaa_SHBb = Shader.PropertyToID(nameof(aaaa_SHBb));
            public static readonly int aaaa_SHC = Shader.PropertyToID(nameof(aaaa_SHC));
        }
    }
}