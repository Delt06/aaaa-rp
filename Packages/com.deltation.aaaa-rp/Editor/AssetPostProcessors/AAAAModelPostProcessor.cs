using System;
using System.Collections.Generic;
using System.Linq;
using DELTation.AAAARP.Editor.Meshlets;
using DELTation.AAAARP.Materials;
using DELTation.AAAARP.Meshlets;
using DELTation.AAAARP.Renderers;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DELTation.AAAARP.Editor.AssetPostProcessors
{
    internal sealed class AAAAModelPostProcessor : AAAAAssetPostprocessorBase
    {
        private const int Version = 1;
        private const int Order = 0;

        private void OnPostprocessMeshHierarchy(GameObject g)
        {
            if (!ShouldRun())
            {
                return;
            }

            var modelSettings = AAAAModelSettings.Deserialize(assetImporter.userData);
            if (!modelSettings.GenerateMeshlets)
            {
                return;
            }

            var assetObjects = new List<Object>();
            var allMaterials = new List<Material>();
            var allMeshes = new Dictionary<SubMeshKey, AAAAMeshletCollectionAsset>();

            context.GetObjects(assetObjects);

            const bool includeInactive = true;
            foreach (MeshRenderer meshRenderer in g.GetComponentsInChildren<MeshRenderer>(includeInactive))
            {
                if (meshRenderer.TryGetComponent(out MeshFilter meshFilter))
                {
                    Material[] sharedMaterials = meshRenderer.sharedMaterials;
                    allMaterials.AddRange(sharedMaterials);

                    Mesh sharedMesh = meshFilter.sharedMesh;
                    if (sharedMesh != null)
                    {
                        for (int subMeshIndex = 0; subMeshIndex < sharedMesh.subMeshCount; subMeshIndex++)
                        {
                            GameObject gameObject = meshRenderer.gameObject;
                            if (sharedMesh.subMeshCount > 1)
                            {
                                var subMeshGameObject = new GameObject($"{gameObject.name}__{subMeshIndex:00}");
                                subMeshGameObject.transform.SetParent(gameObject.transform, false);

                                gameObject = subMeshGameObject;
                            }
                            AAAARendererAuthoring rendererAuthoring = gameObject.AddComponent<AAAARendererAuthoring>();
                            rendererAuthoring.LODErrorScale = modelSettings.LODErrorScale;
                            rendererAuthoring.ContributeToBakedGlobalIllumination = modelSettings.ContributeGlobalIllumination;

                            var subMeshKey = new SubMeshKey
                            {
                                Mesh = sharedMesh,
                                SubMeshIndex = subMeshIndex,
                            };
                            if (!allMeshes.TryGetValue(subMeshKey, out AAAAMeshletCollectionAsset meshletCollectionAsset))
                            {
                                meshletCollectionAsset = ScriptableObject.CreateInstance<AAAAMeshletCollectionAsset>();
                                meshletCollectionAsset.name = sharedMesh.name + $"_{subMeshIndex:00}";

                                AAAAMeshletCollectionBuilder.Generate(meshletCollectionAsset, new AAAAMeshletCollectionBuilder.Parameters
                                    {
                                        Mesh = sharedMesh,
                                        SourceMeshGUID = AssetDatabase.AssetPathToGUID(assetImporter.assetPath),
                                        SubMeshIndex = subMeshIndex,
                                        LogErrorHandler = e => context.LogImportError(e),
                                        TargetError = 0.02f,
                                        TargetErrorSloppy = 0.0f,

                                        // OptimizeVertexCache = true,
                                        MinTriangleReductionPerStep = 0.9f,
                                        MaxMeshLODLevelCount = 0,
                                    }
                                );

                                context.AddObjectToAsset(meshletCollectionAsset.name + "_" + nameof(AAAAMeshletCollectionAsset), meshletCollectionAsset);
                                allMeshes.Add(subMeshKey, meshletCollectionAsset);
                            }

                            rendererAuthoring.Mesh = meshletCollectionAsset;

                            {
                                Material material = sharedMaterials[math.min(subMeshIndex, sharedMaterials.Length - 1)];
                                if (material != null)
                                {
                                    AAAAMaterialAsset materialAsset = null;

                                    foreach (AAAAModelSettings.MaterialMapping materialMapping in modelSettings.RemapMaterials)
                                    {
                                        if (materialMapping.MaterialAsset != null && materialMapping.Name == material.name)
                                        {
                                            materialAsset = materialMapping.MaterialAsset;
                                            break;
                                        }
                                    }

                                    if (materialAsset == null)
                                    {
                                        materialAsset = assetObjects.OfType<AAAAMaterialAsset>().FirstOrDefault(o => o.name == material.name);
                                    }

                                    rendererAuthoring.Material = materialAsset;
                                }
                            }

                        }
                    }

                    Object.DestroyImmediate(meshFilter);
                }

                Object.DestroyImmediate(meshRenderer);
            }

            if (modelSettings.CleanupDefaultMaterials)
            {
                foreach (Material material in allMaterials)
                {
                    // Internal materials should not have paths yet, but external remapped ones should.
                    // Only delete the internal ones.
                    string materialAssetPath = AssetDatabase.GetAssetPath(material);
                    if (!string.IsNullOrWhiteSpace(materialAssetPath))
                    {
                        continue;
                    }

                    const bool allowDestroyingAssets = true;
                    Object.DestroyImmediate(material, allowDestroyingAssets);
                }
            }
        }

        public override uint GetVersion() => Version;
        public override int GetPostprocessOrder() => Order;

        private struct SubMeshKey : IEquatable<SubMeshKey>
        {
            public Mesh Mesh;
            public int SubMeshIndex;

            public bool Equals(SubMeshKey other) => Equals(Mesh, other.Mesh) && SubMeshIndex == other.SubMeshIndex;

            public override bool Equals(object obj) => obj is SubMeshKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Mesh != null ? Mesh.GetHashCode() : 0) * 397 ^ SubMeshIndex;
                }
            }
        }
    }
}