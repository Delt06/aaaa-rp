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
        private Hash128 _previousReflectionProbeHash;

        public TextureHandle BRDFLut;
        public bool BRDFLutIsDirty;

        public TextureHandle DiffuseIrradiance;
        public RenderTextureDescriptor DiffuseIrradianceDesc;
        public bool DiffuseIrradianceIsDirty;

        public TextureHandle PreFilteredEnvironmentMap;
        public RenderTextureDescriptor PreFilteredEnvironmentMapDesc;
        public bool PreFilteredEnvironmentMapIsDirty;

        public static (Texture reflectionProbe, Vector4 hdrDecodeValues) GetDefaultProbe() =>
            (ReflectionProbe.defaultTexture, ReflectionProbe.defaultTextureHDRDecodeValues);

        public void Init(AAAAImageBasedLightingSettings settings, RenderGraph renderGraph)
        {
            Hash128 currentReflectionProbeHash = GetCurrentReflectionProbeHash();

            if (_previousReflectionProbeHash != currentReflectionProbeHash)
            {
                DiffuseIrradianceIsDirty = true;
                PreFilteredEnvironmentMapIsDirty = true;
                _previousReflectionProbeHash = currentReflectionProbeHash;
            }

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
                DiffuseIrradianceIsDirty = true;
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

        private static Hash128 GetCurrentReflectionProbeHash()
        {
            Texture reflectionProbe = GetDefaultProbe().reflectionProbe;
#if UNITY_EDITOR
            return reflectionProbe.imageContentsHash;
#else
            return new Hash128(math.asuint(reflectionProbe.GetInstanceID()), 0, 0, 0);
#endif
        }

        public override void Reset()
        {
            DiffuseIrradiance = TextureHandle.nullHandle;
            DiffuseIrradianceDesc = default;
            DiffuseIrradianceIsDirty = true;

            BRDFLut = TextureHandle.nullHandle;
            BRDFLutIsDirty = true;

            PreFilteredEnvironmentMap = TextureHandle.nullHandle;
            PreFilteredEnvironmentMapIsDirty = true;
        }
    }
}