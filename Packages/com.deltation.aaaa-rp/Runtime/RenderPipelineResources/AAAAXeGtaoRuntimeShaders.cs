using System;
using DELTation.AAAARP.Data;
using UnityEngine;
using UnityEngine.Categorization;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.RenderPipelineResources
{
    [Serializable]
    [SupportedOnRenderPipeline(typeof(AAAARenderPipelineAsset))]
    [CategoryInfo(Name = "R: XeGTAO Runtime Shaders", Order = 1500)]
    public class AAAAXeGtaoRuntimeShaders : IRenderPipelineResources
    {
        [SerializeField] [HideInInspector] private int _version;

        [SerializeField]
        [ResourcePath("Shaders/GlobalIllumination/XeGTAO/XeGTAO_PrefilterDepths16x16.compute")]
        private ComputeShader _prefilterDepthsCS;

        [SerializeField]
        [ResourcePath("Shaders/GlobalIllumination/XeGTAO/XeGTAO_MainPass.compute")]
        private ComputeShader _mainPassCS;

        [SerializeField]
        [ResourcePath("Shaders/GlobalIllumination/XeGTAO/XeGTAO_Denoise.compute")]
        private ComputeShader _denoiseCS;

        public ComputeShader PrefilterDepthsCS
        {
            get => _prefilterDepthsCS;
            set => this.SetValueAndNotify(ref _prefilterDepthsCS, value, nameof(_prefilterDepthsCS));
        }

        public ComputeShader MainPassCS
        {
            get => _mainPassCS;
            set => this.SetValueAndNotify(ref _mainPassCS, value, nameof(_mainPassCS));
        }

        public ComputeShader DenoiseCS
        {
            get => _denoiseCS;
            set => this.SetValueAndNotify(ref _denoiseCS, value, nameof(_denoiseCS));
        }

        public int version => _version;

        bool IRenderPipelineGraphicsSettings.isAvailableInPlayerBuild => true;
    }
}