using System;
using DELTation.AAAARP.Data;
using UnityEngine;
using UnityEngine.Categorization;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.RenderPipelineResources
{
    [Serializable]
    [SupportedOnRenderPipeline(typeof(AAAARenderPipelineAsset))]
    [CategoryInfo(Name = "R: FSR Runtime Shaders", Order = 1000)]
    public class AAAAFsrRuntimeShaders : IRenderPipelineResources
    {
        [SerializeField] [HideInInspector] private int _version;

        [SerializeField]
        [ResourcePath("Shaders/PostProcessing/FSR/EASU.compute")]
        private ComputeShader _easuCS;

        [SerializeField]
        [ResourcePath("Shaders/PostProcessing/FSR/RCAS.compute")]
        private ComputeShader _rcasCS;

        public ComputeShader EasuCS
        {
            get => _easuCS;
            set => this.SetValueAndNotify(ref _easuCS, value, nameof(_easuCS));
        }

        public ComputeShader RcasCS
        {
            get => _rcasCS;
            set => this.SetValueAndNotify(ref _rcasCS, value, nameof(_rcasCS));
        }

        public int version => _version;

        bool IRenderPipelineGraphicsSettings.isAvailableInPlayerBuild => true;
    }
}