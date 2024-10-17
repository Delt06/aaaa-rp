using System.Collections.Generic;
using System.Linq;
using DELTation.AAAARP.Data;
using DELTation.AAAARP.Materials;
using DELTation.AAAARP.Renderers;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace DELTation.AAAARP.Editor.AssetPostProcessors
{
    internal sealed class AAAAModelPostProcessor : AssetPostprocessor
    {
        private const int Version = 1;
        private const int Order = 0;

        private void OnPostprocessModel(GameObject g)
        {
            if (GraphicsSettings.currentRenderPipeline is not AAAARenderPipelineAsset)
            {
                return;
            }

            using PooledObject<List<Object>> _ = ListPool<Object>.Get(out List<Object> objects);
            using PooledObject<List<Material>> __ = ListPool<Material>.Get(out List<Material> materials);

            context.GetObjects(objects);

            const bool includeInactive = true;
            foreach (MeshRenderer meshRenderer in g.GetComponentsInChildren<MeshRenderer>(includeInactive))
            {
                AAAARendererAuthoring rendererAuthoring = meshRenderer.gameObject.AddComponent<AAAARendererAuthoring>();
                materials.AddRange(meshRenderer.sharedMaterials);
                Material material = meshRenderer.sharedMaterial;
                if (material != null)
                {
                    rendererAuthoring.Material = objects.OfType<AAAAMaterialAsset>().FirstOrDefault(o => o.name == material.name);
                }

                Object.DestroyImmediate(meshRenderer);
            }

            foreach (Material material in materials)
            {
                const bool allowDestroyingAssets = true;
                Object.DestroyImmediate(material, allowDestroyingAssets);
            }
        }

        public override uint GetVersion() => Version;
        public override int GetPostprocessOrder() => Order;
    }
}