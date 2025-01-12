using System;
using DELTation.AAAARP.Lighting;
using DELTation.AAAARP.Renderers;
using UnityEngine.Experimental.Rendering;

namespace DELTation.AAAARP
{
    public readonly struct AAAARenderTexturePoolSet : IDisposable
    {
        public readonly AAAARenderTexturePool ShadowMap;

        internal AAAARenderTexturePoolSet(BindlessTextureContainer bindlessTextureContainer) =>
            ShadowMap = new AAAARenderTexturePool(bindlessTextureContainer, new AAAARenderTexturePool.Parameters
                {
                    NamePrefix = "ShadowMap",
                    DepthFormat = GraphicsFormat.D32_SFloat,
                }
            );

        public void OnPreRender()
        {
            ShadowMap.Reset();
        }

        public void Dispose()
        {
            ShadowMap.Dispose();
        }
    }
}