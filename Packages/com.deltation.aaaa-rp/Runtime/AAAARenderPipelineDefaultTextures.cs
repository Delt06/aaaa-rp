using System;
using DELTation.AAAARP.Data;
using UnityEngine;
using UnityEngine.Categorization;
using UnityEngine.Rendering;

namespace DELTation.AAAARP
{
    [Serializable]
    [SupportedOnRenderPipeline(typeof(AAAARenderPipelineAsset))]
    [CategoryInfo(Name = "R: Default Textures", Order = 1000)]
    public class AAAARenderPipelineDefaultTextures : IRenderPipelineResources
    {
        [SerializeField] [HideInInspector] private int _version;

        [SerializeField]
        [ResourcePath("Assets/Textures/UVTest.jpg")]
        private Texture2D _uvTest;

        public Texture2D UVTest
        {
            get => _uvTest;
            set => this.SetValueAndNotify(ref _uvTest, value, nameof(_uvTest));
        }

        public int version => _version;

        public bool isAvailableInPlayerBuild => true;
    }
}