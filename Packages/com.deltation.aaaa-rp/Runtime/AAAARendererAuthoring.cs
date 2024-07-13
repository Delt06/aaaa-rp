using DELTation.AAAARP.Materials;
using DELTation.AAAARP.Meshlets;
using UnityEngine;

namespace DELTation.AAAARP
{
    public class AAAARendererAuthoring : AAAARendererAuthoringBase
    {
        [SerializeField] private AAAAMeshletCollectionAsset _mesh;
        [SerializeField] private AAAAMaterialAsset _material;

        public override AAAAMeshletCollectionAsset Mesh => _mesh;

        public override AAAAMaterialAsset Material => _material;
    }
}