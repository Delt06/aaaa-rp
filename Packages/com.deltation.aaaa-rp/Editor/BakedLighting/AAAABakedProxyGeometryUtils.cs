using DELTation.AAAARP.BakedLighting;
using DELTation.AAAARP.Materials;
using DELTation.AAAARP.Renderers;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Editor.BakedLighting
{
    internal static class AAAABakedProxyGeometryUtils
    {
        [MenuItem("Tools/AAAA RP/Baked Lighting/Generate Proxy Geometry")]
        public static void Generate()
        {
            AAAARendererAuthoring[] sourceRenderers = Object.FindObjectsByType<AAAARendererAuthoring>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            const HideFlags hideFlags = HideFlags.DontSave;
            var parent = new GameObject("Baked Lighting Proxy Geometry")
            {
                hideFlags = hideFlags,
            };
            parent.AddComponent<AAAABakedProxyGeometry>();

            foreach (AAAARendererAuthoring renderer in sourceRenderers)
            {
                if (renderer.Mesh == null || renderer.Material == null || !renderer.ContributeGlobalIllumination)
                {
                    continue;
                }

                Mesh sourceMesh = LoadAssetByGUIDAndName<Mesh>(renderer.Mesh.SourceMeshGUID, renderer.Mesh.SourceMeshName);
                if (sourceMesh == null)
                {
                    continue;
                }

                var go = new GameObject(renderer.name)
                {
                    hideFlags = hideFlags,
                    transform =
                    {
                        parent = parent.transform,
                    },
                };

                AAAABakedProxyGeometry bakedProxyGeometry = go.AddComponent<AAAABakedProxyGeometry>();
                bakedProxyGeometry.Renderer = renderer;

                var material = new Material(GraphicsSettings.currentRenderPipeline.defaultMaterial)
                {
                    name = renderer.Material.name + $"_{renderer.Mesh.SourceSubmeshIndex:00}/{sourceMesh.subMeshCount:00}",
                };
                SetupMaterial(material, renderer.Material);

                MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
                SetupMeshRenderer(meshRenderer, material, sourceMesh, renderer);

                MeshFilter meshFilter = go.AddComponent<MeshFilter>();
                SetupMeshFilter(meshFilter, sourceMesh);
            }
        }

        private static void SetupMaterial(Material material, AAAAMaterialAsset materialAsset)
        {
            material.color = materialAsset.AlbedoColor;
            material.mainTexture = materialAsset.Albedo;
        }

        private static void SetupMeshRenderer(MeshRenderer meshRenderer, Material material, Mesh sourceMesh, AAAARendererAuthoring renderer)
        {
            meshRenderer.sharedMaterials = CreateMaterialArray(material, sourceMesh.subMeshCount, renderer.Mesh.SourceSubmeshIndex);
            meshRenderer.shadowCastingMode = renderer.ShadowCastingMode;
            meshRenderer.receiveGI = ReceiveGI.LightProbes;
            GameObjectUtility.SetStaticEditorFlags(meshRenderer.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);
        }

        private static void SetupMeshFilter(MeshFilter meshFilter, Mesh sourceMesh)
        {
            meshFilter.sharedMesh = sourceMesh;
        }

        private static Material[] CreateMaterialArray(Material material, int submeshCount, int submeshIndex)
        {
            var materials = new Material[submeshCount];
            materials[submeshIndex] = material;
            return materials;
        }

        [CanBeNull]
        private static T LoadAssetByGUIDAndName<T>(string guid, string sourceMeshName) where T : Object
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (Object asset in assets)
            {
                if (asset is not T castedAsset ||
                    asset.name != sourceMeshName ||
                    !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string foundGuid, out long _) ||
                    foundGuid != guid)
                {
                    continue;
                }

                return castedAsset;
            }

            return null;
        }
    }
}