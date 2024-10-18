using System.Collections.Generic;
using System.Linq;
using DELTation.AAAARP.Editor.Meshlets;
using DELTation.AAAARP.Materials;
using DELTation.AAAARP.Meshlets;
using DELTation.AAAARP.Renderers;
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

            var assetObjects = new List<Object>();
            var allMaterials = new List<Material>();
            var allMeshes = new Dictionary<Mesh, AAAAMeshletCollectionAsset>();

            context.GetObjects(assetObjects);

            const bool includeInactive = true;
            foreach (MeshRenderer meshRenderer in g.GetComponentsInChildren<MeshRenderer>(includeInactive))
            {
                AAAARendererAuthoring rendererAuthoring = meshRenderer.gameObject.AddComponent<AAAARendererAuthoring>();

                if (meshRenderer.TryGetComponent(out MeshFilter meshFilter))
                {
                    Mesh sharedMesh = meshFilter.sharedMesh;
                    if (sharedMesh != null)
                    {
                        if (!allMeshes.TryGetValue(sharedMesh, out AAAAMeshletCollectionAsset meshletCollectionAsset))
                        {
                            meshletCollectionAsset = ScriptableObject.CreateInstance<AAAAMeshletCollectionAsset>();
                            meshletCollectionAsset.name = sharedMesh.name;

                            AAAAMeshletCollectionBuilder.Generate(meshletCollectionAsset, new AAAAMeshletCollectionBuilder.Parameters
                                {
                                    Mesh = sharedMesh,
                                    LogErrorHandler = e => context.LogImportError(e),
                                    OptimizeIndexing = true,
                                    TargetError = 0.02f,
                                    TargetErrorSloppy = 0.0f,
                                    OptimizeVertexCache = true,
                                    MinTriangleReductionPerStep = 0.9f,
                                    MaxMeshLODLevelCount = 0,
                                }
                            );

                            context.AddObjectToAsset(meshletCollectionAsset.name + "_" + nameof(AAAAMeshletCollectionAsset), meshletCollectionAsset);
                            allMeshes.Add(sharedMesh, meshletCollectionAsset);
                        }

                        rendererAuthoring.Mesh = meshletCollectionAsset;
                    }

                    Object.DestroyImmediate(meshFilter);
                }

                {
                    allMaterials.AddRange(meshRenderer.sharedMaterials);
                    Material material = meshRenderer.sharedMaterial;
                    if (material != null)
                    {
                        rendererAuthoring.Material = assetObjects.OfType<AAAAMaterialAsset>().FirstOrDefault(o => o.name == material.name);
                    }
                }

                Object.DestroyImmediate(meshRenderer);
            }

            AAAAModelSettings modelSettings = JsonUtility.FromJson<AAAAModelSettings>(assetImporter.userData);
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

            if (modelSettings.CleanupDefaultMeshes)
            {
                foreach (Mesh mesh in allMeshes.Keys)
                {
                    const bool allowDestroyingAssets = true;
                    Object.DestroyImmediate(mesh, allowDestroyingAssets);
                }
            }
        }

        public override uint GetVersion() => Version;
        public override int GetPostprocessOrder() => Order;
    }
}