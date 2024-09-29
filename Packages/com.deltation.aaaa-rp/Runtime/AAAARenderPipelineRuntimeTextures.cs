using System;
using DELTation.AAAARP.Data;
using UnityEngine;
using UnityEngine.Categorization;
using UnityEngine.Rendering;

namespace DELTation.AAAARP
{
    [Serializable]
    [SupportedOnRenderPipeline(typeof(AAAARenderPipelineAsset))]
    [CategoryInfo(Name = "R: Runtime Textures", Order = 1000)]
    public class AAAARenderPipelineRuntimeTextures : IRenderPipelineResources
    {
        [SerializeField] [HideInInspector] private int _version;

        [SerializeField]
        [ResourcePath("Assets/Textures/UVTest.jpg")]
        private Texture2D _uvTest;

        [SerializeField]
        [ResourcePath("Assets/Textures/SMAA/SearchTex.tga")]
        private Texture2D _smaaSearchTex;

        [SerializeField]
        [ResourcePath("Assets/Textures/SMAA/AreaTex.tga")]
        private Texture2D _smaaAreaTex;

        public Texture2D UVTest
        {
            get => _uvTest;
            set => this.SetValueAndNotify(ref _uvTest, value, nameof(_uvTest));
        }

        public Texture2D SMAASearchTex
        {
            get => _smaaSearchTex;
            set => this.SetValueAndNotify(ref _smaaSearchTex, value, nameof(_smaaSearchTex));
        }

        public Texture2D SMAAAreaTex
        {
            get => _smaaAreaTex;
            set => this.SetValueAndNotify(ref _smaaAreaTex, value, nameof(_smaaAreaTex));
        }

        public int version => _version;

        public bool isAvailableInPlayerBuild => true;
    }
}