using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.FrameData;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes
{
    public class SetupLightingPass : AAAARenderPass<SetupLightingPass.PassData>
    {
        public SetupLightingPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent) { }

        public override string Name => "SetupLighting";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();

            foreach (VisibleLight visibleLight in renderingData.CullingResults.visibleLights)
            {
                if (visibleLight.lightType == LightType.Directional)
                {
                    passData.MainLightColor = visibleLight.finalColor;
                    passData.MainLightDirection = (visibleLight.localToWorldMatrix * Vector3.back).normalized;
                }
            }
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            context.cmd.SetGlobalVector(ShaderPropertyID._MainLight_Color, data.MainLightColor);
            context.cmd.SetGlobalVector(ShaderPropertyID._MainLight_Direction, data.MainLightDirection);

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
            public Vector4 MainLightColor;
            public Vector4 MainLightDirection;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderPropertyID
        {
            public static readonly int _MainLight_Color = Shader.PropertyToID(nameof(_MainLight_Color));
            public static readonly int _MainLight_Direction = Shader.PropertyToID(nameof(_MainLight_Direction));

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