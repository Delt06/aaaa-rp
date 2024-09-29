using DELTation.AAAARP.Data;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.FrameData
{
    public class AAAAImageBasedLightingData : ContextItem
    {
        public TextureHandle BRDFLut;
        public bool BRDFLutIsDirty;

        public TextureHandle DiffuseIrradiance;
        public SphericalHarmonicsL2? DiffuseIrradianceAmbientProbe;
        public RenderTextureDescriptor DiffuseIrradianceDesc;

        public TextureHandle PreFilteredEnvironmentMap;
        public RenderTextureDescriptor PreFilteredEnvironmentMapDesc;
        public bool PreFilteredEnvironmentMapIsDirty;

        public void Init(AAAAImageBasedLightingSettings settings, RenderGraph renderGraph)
        {
            if (!DiffuseIrradiance.IsValid())
            {
                int resolution = (int) settings.DiffuseIrradianceResolution;
                const int depthBufferBits = 0;
                const int mipCount = 1;
                DiffuseIrradianceDesc = new RenderTextureDescriptor(resolution, resolution, GraphicsFormat.R16G16B16A16_SFloat, depthBufferBits, mipCount)
                {
                    dimension = TextureDimension.Cube,
                };
                DiffuseIrradiance = renderGraph.CreateSharedTexture(AAAARenderingUtils.CreateTextureDesc(nameof(DiffuseIrradiance), DiffuseIrradianceDesc));
                DiffuseIrradianceAmbientProbe = default;
            }

            if (!BRDFLut.IsValid())
            {
                int resolution = (int) settings.BRDFLutResolution;
                const int depthBufferBits = 0;
                const int mipCount = 1;
                var desc = new RenderTextureDescriptor(resolution, resolution, GraphicsFormat.R16G16_SFloat, depthBufferBits, mipCount)
                {
                    dimension = TextureDimension.Tex2D,
                };
                BRDFLut = renderGraph.CreateSharedTexture(AAAARenderingUtils.CreateTextureDesc(nameof(BRDFLut), desc));
                BRDFLutIsDirty = true;
            }

            if (!PreFilteredEnvironmentMap.IsValid())
            {
                AAAAImageBasedLightingSettings.PreFilteredEnvironmentMapSettings preFilteredEnvironmentMapSettings = settings.PreFilteredEnvironmentMap;
                int resolution = (int) preFilteredEnvironmentMapSettings.Resolution;
                const int depthBufferBits = 0;
                int mipCount = preFilteredEnvironmentMapSettings.MaxMipLevels;

                int maxMipCountForResolution = (int) math.log2(resolution) + 1;
                mipCount = math.min(mipCount, maxMipCountForResolution);

                PreFilteredEnvironmentMapDesc =
                    new RenderTextureDescriptor(resolution, resolution, GraphicsFormat.R16G16B16A16_SFloat, depthBufferBits, mipCount)
                    {
                        dimension = TextureDimension.Cube,
                        useMipMap = true,
                        autoGenerateMips = false,
                    };
                PreFilteredEnvironmentMap =
                    renderGraph.CreateSharedTexture(AAAARenderingUtils.CreateTextureDesc(nameof(PreFilteredEnvironmentMap), PreFilteredEnvironmentMapDesc));

                PreFilteredEnvironmentMapIsDirty = true;
            }
        }

        public override void Reset()
        {
            DiffuseIrradiance = TextureHandle.nullHandle;
            DiffuseIrradianceDesc = default;
            DiffuseIrradianceAmbientProbe = default;

            BRDFLut = TextureHandle.nullHandle;
            BRDFLutIsDirty = true;

            PreFilteredEnvironmentMap = TextureHandle.nullHandle;
            PreFilteredEnvironmentMapIsDirty = true;
        }
    }
}