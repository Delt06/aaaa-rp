using System;
using System.Collections.Generic;
using DELTation.AAAARP.Materials;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace DELTation.AAAARP.Editor.AssetPostProcessors
{
    [Serializable]
    public class AAAAModelSettings
    {
        public bool GenerateMaterialAssets = true;
        public bool CleanupDefaultMaterials = true;
        public bool CleanupDefaultMeshes = true;
        public List<MaterialMapping> RemapMaterials = new();

        [HideInInspector]
        public List<MaterialMappingGuid> RemapMaterialsGuids = new();

        [MustUseReturnValue]
        public static string Serialize(AAAAModelSettings modelSettings)
        {
            modelSettings.RemapMaterialsGuids.Clear();

            foreach (MaterialMapping remapMaterial in modelSettings.RemapMaterials)
            {
                string assetPath = AssetDatabase.GetAssetPath(remapMaterial.MaterialAsset);
                string guid = AssetDatabase.AssetPathToGUID(assetPath);

                int existingIndex = modelSettings.RemapMaterialsGuids.FindIndex(m => m.Name == remapMaterial.Name);
                if (existingIndex == -1)
                {
                    modelSettings.RemapMaterialsGuids.Add(new MaterialMappingGuid
                        {
                            Name = remapMaterial.Name,
                            Guid = guid,
                        }
                    );
                }
                else
                {
                    MaterialMappingGuid existingMapping = modelSettings.RemapMaterialsGuids[existingIndex];
                    if (string.IsNullOrWhiteSpace(existingMapping.Guid))
                    {
                        existingMapping.Guid = guid;
                    }
                    modelSettings.RemapMaterialsGuids[existingIndex] = existingMapping;
                }
            }

            modelSettings.RemapMaterials.Clear();

            return JsonUtility.ToJson(modelSettings);
        }

        [MustUseReturnValue]
        public static AAAAModelSettings Deserialize(string serializedSettings)
        {
            var modelSettings = new AAAAModelSettings();
            Deserialize(serializedSettings, modelSettings);
            return modelSettings;
        }

        public static void Deserialize(string serializedSettings, AAAAModelSettings modelSettings)
        {
            JsonUtility.FromJsonOverwrite(serializedSettings, modelSettings);
            modelSettings.RemapMaterials.Clear();

            foreach (MaterialMappingGuid remapMaterialsGuid in modelSettings.RemapMaterialsGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(remapMaterialsGuid.Guid);
                AAAAMaterialAsset materialAsset = AssetDatabase.LoadAssetAtPath<AAAAMaterialAsset>(assetPath);
                modelSettings.RemapMaterials.Add(new MaterialMapping
                    {
                        Name = remapMaterialsGuid.Name,
                        MaterialAsset = materialAsset,
                    }
                );
            }
        }

        [Serializable]
        public struct MaterialMapping
        {
            public string Name;
            public AAAAMaterialAsset MaterialAsset;
        }

        [Serializable]
        public struct MaterialMappingGuid
        {
            public string Name;
            public string Guid;
        }
    }
}