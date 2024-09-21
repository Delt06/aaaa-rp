using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace DELTation.AAAARP
{
    public class BindlessTextureContainer
    {
        private readonly Dictionary<Texture2D, BindlessTextureInfo> _bindlessTextureInfos = new();
        private readonly uint _heapNumDescriptors = DELTationBindlessPlugin.GetSRVDescriptorHeapCount();
        private uint _counter;

        public void PreRender()
        {
            using ObjectPool<List<Texture2D>>.PooledObject _ = ListPool<Texture2D>.Get(out List<Texture2D> dirtyTextures);

            foreach ((Texture2D texture, BindlessTextureInfo bindlessTextureInfo) in _bindlessTextureInfos)
            {
                if (texture.GetNativeTexturePtr() != bindlessTextureInfo.NativeTexturePtr)
                {
                    dirtyTextures.Add(texture);
                }
            }

            foreach (Texture2D dirtyTexture in dirtyTextures)
            {
                GetOrCreateIndex(dirtyTexture);
            }
        }

        public uint GetOrCreateIndex(Texture2D texture)
        {
            if (texture == null)
            {
                texture = Texture2D.whiteTexture;
            }

            uint index;

            IntPtr nativeTexturePtr = texture.GetNativeTexturePtr();
            if (_bindlessTextureInfos.TryGetValue(texture, out BindlessTextureInfo info))
            {
                if (info.NativeTexturePtr == nativeTexturePtr)
                {
                    return info.Index;
                }

                index = info.Index;
            }
            else
            {
                index = _heapNumDescriptors - 1 - _counter;
                ++_counter;
            }

            int result = DELTationBindlessPlugin.CreateSRVDescriptor(nativeTexturePtr, index);
            Assert.IsTrue(result == 0);

            _bindlessTextureInfos[texture] = new BindlessTextureInfo
            {
                Index = index,
                NativeTexturePtr = nativeTexturePtr,
            };
            return index;
        }

        private struct BindlessTextureInfo
        {
            public uint Index;
            public IntPtr NativeTexturePtr;
        }
    }
}