using System;
using System.Collections.Generic;
using DELTation.AAAARP.Renderers;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Lighting
{
    internal sealed class ShadowMapPool : IDisposable
    {
        private readonly BindlessTextureContainer _bindlessTextureContainer;
        private readonly Dictionary<int, PoolData> _resolutionPools = new();

        public ShadowMapPool(BindlessTextureContainer bindlessTextureContainer) => _bindlessTextureContainer = bindlessTextureContainer;

        public void Dispose()
        {
            foreach (PoolData poolData in _resolutionPools.Values)
            {
                foreach (RenderTexture renderTexture in poolData.RenderTextures)
                {
                    CoreUtils.Destroy(renderTexture);
                }
            }
            _resolutionPools.Clear();
        }

        public void Reset()
        {
            foreach (PoolData poolData in _resolutionPools.Values)
            {
                poolData.Offset = 0;
            }
        }

        public Allocation Allocate(int resolution)
        {
            if (!_resolutionPools.TryGetValue(resolution, out PoolData poolData))
            {
                _resolutionPools[resolution] = poolData = new PoolData
                {
                    RenderTextures = new List<RenderTexture>(),
                    Offset = 0,
                };
            }

            while (poolData.Offset >= poolData.RenderTextures.Count)
            {
                var renderTexture = new RenderTexture(resolution, resolution, GraphicsFormat.None, GraphicsFormat.D32_SFloat, 1)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
#if DEBUG
                renderTexture.name = $"ShadowMap_{resolution}x{resolution}_{poolData.RenderTextures.Count:00}";
#endif

                renderTexture.Create();

                poolData.RenderTextures.Add(renderTexture);
            }

            var allocation = new Allocation
            {
                Index = poolData.Offset,
                Resolution = resolution,
            };
            poolData.Offset++;
            return allocation;
        }

        [MustUseReturnValue]
        public RenderTexture LookupRenderTexture(Allocation allocation) => _resolutionPools[allocation.Resolution].RenderTextures[allocation.Index];

        public int GetBindlessSRVIndexOrDefault(Allocation allocation, int defaultSRVIndex)
        {
            RenderTexture renderTexture = LookupRenderTexture(allocation);
            if (renderTexture.IsCreated() && renderTexture.GetNativeDepthBufferPtr() != IntPtr.Zero)
            {
                return (int) _bindlessTextureContainer.GetOrCreateIndex(renderTexture, renderTexture.GetInstanceID());
            }
            return defaultSRVIndex;
        }

        public struct Allocation
        {
            public int Resolution;
            public int Index;
        }

        private class PoolData
        {
            public int Offset;
            public List<RenderTexture> RenderTextures;
        }
    }
}