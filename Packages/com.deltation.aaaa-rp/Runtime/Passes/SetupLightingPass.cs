using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.FrameData;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes
{
    public class SetupLightingPassData : PassDataBase
    {
        public Vector4 MainLightColor;
        public Vector4 MainLightDirection;
    }

    public class SetupLightingPass : AAAARenderPass<SetupLightingPassData>
    {
        public SetupLightingPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent) { }

        public override string Name => "SetupLighting";

        protected override void Setup(RenderGraphBuilder builder, SetupLightingPassData passData, ContextContainer frameData)
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

        protected override void Render(SetupLightingPassData data, RenderGraphContext context)
        {
            context.cmd.SetGlobalVector(ShaderPropertyID._MainLight_Color, data.MainLightColor);
            context.cmd.SetGlobalVector(ShaderPropertyID._MainLight_Direction, data.MainLightDirection);
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderPropertyID
        {
            public static readonly int _MainLight_Color = Shader.PropertyToID(nameof(_MainLight_Color));
            public static readonly int _MainLight_Direction = Shader.PropertyToID(nameof(_MainLight_Direction));
        }
    }
}