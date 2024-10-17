using System.Runtime.CompilerServices;
using DELTation.AAAARP.Core.ObjectDispatching;
using DELTation.AAAARP.Materials;
using DELTation.AAAARP.Meshlets;
using UnityEngine;

namespace DELTation.AAAARP.Renderers
{
    public sealed class AAAARendererAuthoring : MonoBehaviour
    {
        [SerializeField] private AAAAMeshletCollectionAsset _mesh;
        [SerializeField] private AAAAMaterialAsset _material;
        [SerializeField] [Min(0.000001f)] private float _lodErrorScale = 1;

        public AAAAMeshletCollectionAsset Mesh
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _mesh;
            set
            {
                _mesh = value;
                UnityObjectUtils.MarkDirty(this);
            }
        }

        public AAAAMaterialAsset Material
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _material;
            set
            {
                _material = value;
                UnityObjectUtils.MarkDirty(this);
            }
        }

        public float LODErrorScale
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _lodErrorScale;
            set
            {
                _lodErrorScale = value;
                UnityObjectUtils.MarkDirty(this);
            }
        }
    }
}