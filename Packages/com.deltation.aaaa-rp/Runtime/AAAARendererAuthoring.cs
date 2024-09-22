using DELTation.AAAARP.Materials;
using DELTation.AAAARP.Meshlets;
using UnityEngine;

namespace DELTation.AAAARP
{
    public sealed class AAAARendererAuthoring : MonoBehaviour
    {
        [SerializeField] private AAAAMeshletCollectionAsset _mesh;
        [SerializeField] private AAAAMaterialAsset _material;

        public AAAAMeshletCollectionAsset Mesh => _mesh;

        public AAAAMaterialAsset Material => _material;
    }
}