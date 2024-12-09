using System;
using DELTation.AAAARP.Lighting;
using DELTation.AAAARP.Renderers;
using UnityEngine.Experimental.Rendering;

namespace DELTation.AAAARP
{
    public readonly struct AAAARenderTexturePoolSet : IDisposable
    {
        public readonly AAAARenderTexturePool ShadowMap;

        public readonly AAAARenderTexturePool RsmPositionMap;
        public readonly AAAARenderTexturePool RsmNormalMap;
        public readonly AAAARenderTexturePool RsmFluxMap;

        internal AAAARenderTexturePoolSet(BindlessTextureContainer bindlessTextureContainer)
        {
            ShadowMap = new AAAARenderTexturePool(bindlessTextureContainer, new AAAARenderTexturePool.Parameters
                {
                    NamePrefix = "ShadowMap",
                    DepthFormat = GraphicsFormat.D32_SFloat,
                }
            );
            RsmPositionMap = new AAAARenderTexturePool(bindlessTextureContainer, new AAAARenderTexturePool.Parameters
                {
                    NamePrefix = "RSM_PositionMap",
                    ColorFormat = GraphicsFormat.R32G32B32A32_SFloat,
                    EnableRandomWrite = true,
                }
            );
            RsmNormalMap = new AAAARenderTexturePool(bindlessTextureContainer, new AAAARenderTexturePool.Parameters
                {
                    NamePrefix = "RSM_NormalMap",
                    ColorFormat = GraphicsFormat.R16G16_SNorm,
                    EnableRandomWrite = true,
                }
            );

            {
                const bool isHdrEnabled = true;
                const HDRColorBufferPrecision hdrColorBufferPrecision = HDRColorBufferPrecision._32Bits;
                const bool needsAlpha = false;
                RsmFluxMap = new AAAARenderTexturePool(bindlessTextureContainer, new AAAARenderTexturePool.Parameters
                    {
                        NamePrefix = "RSM_FluxMap",
                        ColorFormat = AAAARenderPipelineCore.MakeRenderTextureGraphicsFormat(isHdrEnabled, hdrColorBufferPrecision, needsAlpha),
                        EnableRandomWrite = true,
                    }
                );
            }
        }

        public void OnPreRender()
        {
            ShadowMap.Reset();
            RsmPositionMap.Reset();
            RsmNormalMap.Reset();
            RsmFluxMap.Reset();
        }

        public void Dispose()
        {
            ShadowMap.Dispose();
            RsmPositionMap.Dispose();
            RsmNormalMap.Dispose();
            RsmFluxMap.Dispose();
        }
    }
}