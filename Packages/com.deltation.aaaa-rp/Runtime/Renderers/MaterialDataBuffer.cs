using System;
using System.Collections.Generic;
using DELTation.AAAARP.Materials;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace DELTation.AAAARP.Renderers
{
    internal sealed class MaterialDataBuffer : IDisposable
    {
        private readonly BindlessTextureContainer _bindlessTextureContainer;
        private readonly Dictionary<AAAAMaterialAsset, int> _materialToIndex = new();

        private bool _isDirty = true;
        private NativeList<AAAAMaterialData> _materialData;
        private GraphicsBuffer _materialDataBuffer;

        public MaterialDataBuffer(BindlessTextureContainer bindlessTextureContainer, Allocator allocator)
        {
            _bindlessTextureContainer = bindlessTextureContainer;
            _materialData = new NativeList<AAAAMaterialData>(allocator);
        }

        public void Dispose()
        {
            if (_materialData.IsCreated)
            {
                _materialData.Dispose();
            }

            _materialDataBuffer?.Dispose();
        }

        public void PreRender(CommandBuffer cmd)
        {
            UploadData(cmd);

            cmd.SetGlobalBuffer(RendererContainerShaderIDs._MaterialData, _materialDataBuffer);
        }

        private void UploadData(CommandBuffer cmd)
        {
            if (_isDirty)
            {
                _materialDataBuffer?.Dispose();
                _materialDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                    math.max(1, _materialData.Length), UnsafeUtility.SizeOf<AAAAMaterialData>()
                )
                {
                    name = "MaterialData",
                };
                cmd.SetBufferData(_materialDataBuffer, _materialData.AsArray());
                _isDirty = false;
            }
        }

        public int GetOrAllocateMaterial(AAAAMaterialAsset material)
        {
            if (_materialToIndex.TryGetValue(material, out int index))
            {
                return index;
            }

            AAAAMaterialData materialData = ConvertAssetToData(material);
            _materialData.Add(materialData);
            index = _materialData.Length - 1;
            _materialToIndex.Add(material, index);
            _isDirty = true;
            return index;
        }

        private uint GetOrAllocateTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return AAAAMaterialData.NoTextureIndex;
            }

            return _bindlessTextureContainer.GetOrCreateIndex(texture, texture.GetInstanceID());
        }

        public void OnMaterialAssetsChanged(List<Object> changed, NativeArray<int> changedID)
        {
            for (int changedIndex = 0; changedIndex < changedID.Length; changedIndex++)
            {
                var material = (AAAAMaterialAsset) changed[changedIndex];
                if (_materialToIndex.TryGetValue(material, out int materialIndex))
                {
                    _materialData[materialIndex] = ConvertAssetToData(material);
                    _isDirty = true;
                }
            }
        }

        private AAAAMaterialData ConvertAssetToData(AAAAMaterialAsset material) =>
            new()
            {
                AlbedoColor = (Vector4) material.AlbedoColor,
                AlbedoIndex = GetOrAllocateTexture(material.Albedo),
                TextureTilingOffset = material.TextureTilingOffset,

                NormalsIndex = GetOrAllocateTexture(material.Normals),
                NormalsStrength = material.NormalsStrength,

                MasksIndex = GetOrAllocateTexture(material.Masks),
                Roughness = material.Roughness,
                Metallic = material.Metallic,
                SpecularAAScreenSpaceVariance = material.SpecularAAScreenSpaceVariance,
                SpecularAAThreshold = material.SpecularAAThreshold,

                GeometryFlags = ExtractGeometryFlags(material),
                MaterialFlags = ExtractMaterialFlags(material),
                RendererListID = ConstructRendererListID(material),
                AlphaClipThreshold = material.AlphaClipThreshold,
            };

        private static AAAAGeometryFlags ExtractGeometryFlags(AAAAMaterialAsset material)
        {
            AAAAGeometryFlags flags = AAAAGeometryFlags.None;

            if (material.SpecularAA)
            {
                flags |= AAAAGeometryFlags.SpecularAA;
            }

            return flags;
        }

        private static AAAAMaterialFlags ExtractMaterialFlags(AAAAMaterialAsset material)
        {
            if (material.DisableLighting)
            {
                return AAAAMaterialFlags.Unlit;
            }

            AAAAMaterialFlags flags = AAAAMaterialFlags.None;

            return flags;
        }

        private static AAAARendererListID ConstructRendererListID(AAAAMaterialAsset material)
        {
            AAAARendererListID listID = AAAARendererListID.Default;

            if (material.AlphaClip)
            {
                listID |= AAAARendererListID.AlphaTest;
            }

            if (material.TwoSided)
            {
                listID |= AAAARendererListID.CullOff;
            }

            return listID;
        }
    }
}