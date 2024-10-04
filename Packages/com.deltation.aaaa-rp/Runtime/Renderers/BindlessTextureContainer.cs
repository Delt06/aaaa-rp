using System;
using System.Collections.Generic;
using DELTation.AAAARP.BindlessPlugin.Runtime;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;

namespace DELTation.AAAARP.Renderers
{
    internal sealed class BindlessTextureContainer : IDisposable
    {
        private const int InitialCapacity = 16;

        private readonly uint _heapNumDescriptors = BindlessPluginBindings.GetSRVDescriptorHeapCount();
        private readonly List<Texture> _potentiallyDirtyTextures = new(InitialCapacity);
        private NativeHashMap<int, BindlessTextureInfo> _bindlessTextureInfos = new(InitialCapacity, Allocator.Persistent);
        private uint _counter;
        private NativeList<int> _potentiallyDirtyDestroyedTexturesInstanceID = new(InitialCapacity, Allocator.Persistent);
        private NativeList<int> _potentiallyDirtyTexturesInstanceID = new(InitialCapacity, Allocator.Persistent);

        public void Dispose()
        {
            _bindlessTextureInfos.Dispose();
            _potentiallyDirtyTexturesInstanceID.Dispose();
            _potentiallyDirtyTextures.Clear();
            _potentiallyDirtyDestroyedTexturesInstanceID.Dispose();
        }

        public void AddPotentialDirtyTextureRange(NativeArray<int> textureInstanceIDs, List<Object> textures)
        {
            _potentiallyDirtyTexturesInstanceID.AddRange(textureInstanceIDs);

            foreach (Object texture in textures)
            {
                _potentiallyDirtyTextures.Add((Texture2D) texture);
            }
        }

        public void AddPotentialDestroyedDirtyTextureRange(NativeArray<int> textureInstanceIDs)
        {
            _potentiallyDirtyDestroyedTexturesInstanceID.AddRange(textureInstanceIDs);
        }

        public void PreRender()
        {
            UpdateDirtyTextures();
        }

        private void UpdateDirtyTextures()
        {
            if (_potentiallyDirtyTexturesInstanceID.Length > 0)
            {
                for (int i = 0; i < _potentiallyDirtyTexturesInstanceID.Length; i++)
                {
                    int instanceID = _potentiallyDirtyTexturesInstanceID[i];

                    if (!_bindlessTextureInfos.TryGetValue(instanceID, out BindlessTextureInfo bindlessTextureInfo))
                    {
                        continue;
                    }

                    Texture texture = _potentiallyDirtyTextures[i];
                    Assert.IsNotNull(texture);

                    if (texture.GetNativeTexturePtr() != bindlessTextureInfo.NativeTexturePtr)
                    {
                        GetOrCreateIndex(texture, instanceID);
                    }
                }

                _potentiallyDirtyTexturesInstanceID.Clear();
                _potentiallyDirtyTextures.Clear();
            }

            if (_potentiallyDirtyDestroyedTexturesInstanceID.Length > 0)
            {
                foreach (int instanceID in _potentiallyDirtyDestroyedTexturesInstanceID)
                {
                    if (!_bindlessTextureInfos.TryGetValue(instanceID, out BindlessTextureInfo _))
                    {
                        continue;
                    }

                    GetOrCreateIndex(null, instanceID);
                }

                _potentiallyDirtyDestroyedTexturesInstanceID.Clear();
            }
        }

        public uint GetOrCreateIndex(Texture texture, int instanceID)
        {
            Texture effectiveTexture = texture;

            if (effectiveTexture == null)
            {
                effectiveTexture = Texture2D.whiteTexture;
            }

            uint index;

            IntPtr nativeTexturePtr = effectiveTexture.GetNativeTexturePtr();
            if (_bindlessTextureInfos.TryGetValue(instanceID, out BindlessTextureInfo info))
            {
                if (info.NativeTexturePtr == nativeTexturePtr)
                {
                    return info.Index;
                }

                index = info.Index;
            }
            else
            {
                index = CurrentIndex();
                ++_counter;
            }

            int result = BindlessPluginBindings.CreateSRVDescriptor(nativeTexturePtr, index);
            Assert.IsTrue(result == 0);

            _bindlessTextureInfos[instanceID] = new BindlessTextureInfo
            {
                Index = index,
                NativeTexturePtr = nativeTexturePtr,
            };
            return index;
        }

        private uint CurrentIndex() => _heapNumDescriptors - 1 - _counter;

        public uint AllocateRange(uint count)
        {
            Assert.IsTrue(count > 0);
            _counter += count;
            return CurrentIndex() + 1;
        }

        private struct BindlessTextureInfo
        {
            public uint Index;
            public IntPtr NativeTexturePtr;
        }
    }
}