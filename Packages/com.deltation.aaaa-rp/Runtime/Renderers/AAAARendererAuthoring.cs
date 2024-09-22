﻿using System.Runtime.CompilerServices;
using DELTation.AAAARP.Materials;
using DELTation.AAAARP.Meshlets;
using UnityEngine;

namespace DELTation.AAAARP.Renderers
{
    public sealed class AAAARendererAuthoring : MonoBehaviour
    {
        [SerializeField] private AAAAMeshletCollectionAsset _mesh;
        [SerializeField] private AAAAMaterialAsset _material;

        public AAAAMeshletCollectionAsset Mesh
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _mesh;
        }

        public AAAAMaterialAsset Material
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _material;
        }
    }
}