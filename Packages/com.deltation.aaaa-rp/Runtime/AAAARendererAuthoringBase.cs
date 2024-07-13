using DELTation.AAAARP.Materials;
using DELTation.AAAARP.Meshlets;
using UnityEngine;

namespace DELTation.AAAARP
{
    public abstract class AAAARendererAuthoringBase : MonoBehaviour
    {
        public abstract AAAAMeshletCollectionAsset Mesh { get; }
        public abstract AAAAMaterialAsset Material { get; }
    }
}