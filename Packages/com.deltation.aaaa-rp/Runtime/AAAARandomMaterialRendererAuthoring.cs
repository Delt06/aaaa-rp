using DELTation.AAAARP.Materials;
using DELTation.AAAARP.Meshlets;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DELTation.AAAARP
{
    public class AAAARandomMaterialRendererAuthoring : AAAARendererAuthoringBase
    {
        [SerializeField]
        private AAAAMeshletCollectionAsset _mesh;
        [SerializeField]
        private AAAAMaterialAsset[] _materialPool;

        private int _index;

        public override AAAAMeshletCollectionAsset Mesh => _mesh;
        public override AAAAMaterialAsset Material => _index >= 0 ? _materialPool[Mathf.Clamp(_index, 0, _materialPool.Length - 1)] : null;

        private void Awake()
        {
            _index = _materialPool.Length > 0 ? Random.Range(0, _materialPool.Length) : -1;
        }
    }
}