using System.Collections.Generic;
using DELTation.AAAARP.Materials;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace DELTation.AAAARP.Editor.AssetPostProcessors
{
    internal sealed class AAAAFbxMaterialDescriptionPreprocessor : AAAAAssetPostprocessorBase
    {
        private const int Version = 1;
        private const int Order = -980;

        private void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] animations)
        {
            if (!ShouldRun())
            {
                return;
            }

            AAAAModelSettings modelSettings = JsonUtility.FromJson<AAAAModelSettings>(assetImporter.userData);
            if (!modelSettings.GenerateMaterialAssets)
            {
                return;
            }

            AAAAMaterialAsset materialAsset = ScriptableObject.CreateInstance<AAAAMaterialAsset>();
            materialAsset.name = description.materialName;

            ConvertToMaterialAsset(description, materialAsset);

            context.AddObjectToAsset(materialAsset.name + "_" + nameof(AAAAMaterialAsset), materialAsset);
        }

        public override uint GetVersion() => Version;
        public override int GetPostprocessOrder() => Order;

        private static void ConvertToMaterialAsset(MaterialDescription description, AAAAMaterialAsset materialAsset)
        {
            {
                const float opacity = 1.0f;

                var propertyNames = new List<string>();
                description.GetTexturePropertyNames(propertyNames);

                if (description.TryGetProperty("DiffuseColor", out TexturePropertyDescription diffuseTextureProperty) &&
                    diffuseTextureProperty.texture is Texture2D albedoMap)
                {
                    Color diffuseColor = Color.white;
                    if (description.TryGetProperty("DiffuseFactor", out float diffuseFactorProperty))
                    {
                        diffuseColor *= diffuseFactorProperty;
                    }
                    diffuseColor.a = opacity;

                    materialAsset.Albedo = albedoMap;
                    materialAsset.AlbedoColor = diffuseColor;
                    materialAsset.TextureTilingOffset = new Vector4(
                        diffuseTextureProperty.scale.x, diffuseTextureProperty.scale.y,
                        diffuseTextureProperty.offset.x,
                        diffuseTextureProperty.offset.y
                    );
                }
                else
                {
                    if (description.TryGetProperty("DiffuseColor", out Vector4 diffuseColorProperty))
                    {
                        Color diffuseColor = diffuseColorProperty;
                        diffuseColor.a = 1.0f;
                        materialAsset.AlbedoColor = PlayerSettings.colorSpace == ColorSpace.Linear ? diffuseColor.gamma : diffuseColor;
                    }
                }
            }

            {
                if (
                    (description.TryGetProperty("Bump", out TexturePropertyDescription normalMapProperty) ||
                     description.TryGetProperty("NormalMap", out normalMapProperty)) &&
                    normalMapProperty.texture is Texture2D normalMap)
                {
                    materialAsset.Normals = normalMap;

                    if (description.TryGetProperty("BumpFactor", out float floatProperty))
                    {
                        materialAsset.NormalsStrength = floatProperty;
                    }
                }
            }

            {
                if (description.TryGetProperty("Shininess", out float shininessProperty))
                {
                    float glossiness = Mathf.Sqrt(shininessProperty * 0.01f);
                    materialAsset.Roughness = 1 - glossiness;
                }
                else
                {
                    materialAsset.Roughness = 1;
                }
            }
        }
    }
}