using System;
using DELTation.AAAARP.Data;
using UnityEngine;
using UnityEngine.Categorization;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.RenderPipelineResources
{
    [Serializable]
    [SupportedOnRenderPipeline(typeof(AAAARenderPipelineAsset))]
    [CategoryInfo(Name = "R: Light Propagation Volumes Runtime Shaders", Order = 2000)]
    public class AAAALpvRuntimeShaders : IRenderPipelineResources
    {
        [SerializeField] [HideInInspector] private int _version;

        [SerializeField]
        [ResourcePath("Shaders/GlobalIllumination/RSMDownsample.compute")]
        private ComputeShader _rsmDownsampleCS;

        [SerializeField]
        [ResourcePath("Shaders/GlobalIllumination/LPVInject.shader")]
        private Shader _injectPS;

        [SerializeField]
        [ResourcePath("Shaders/GlobalIllumination/LPVPropagate.compute")]
        private ComputeShader _propagateCS;

        [SerializeField]
        [ResourcePath("Shaders/GlobalIllumination/LPVResolve.compute")]
        private ComputeShader _resolveCS;

        [SerializeField]
        [ResourcePath("Shaders/GlobalIllumination/LPVSkyOcclusion.compute")]
        private ComputeShader _skyOcclusionCS;

        public ComputeShader RsmDownsampleCS
        {
            get => _rsmDownsampleCS;
            set => this.SetValueAndNotify(ref _rsmDownsampleCS, value, nameof(_rsmDownsampleCS));
        }

        public Shader InjectPS
        {
            get => _injectPS;
            set => this.SetValueAndNotify(ref _injectPS, value, nameof(_injectPS));
        }

        public ComputeShader PropagateCS
        {
            get => _propagateCS;
            set => this.SetValueAndNotify(ref _propagateCS, value, nameof(_propagateCS));
        }

        public ComputeShader ResolveCS
        {
            get => _resolveCS;
            set => this.SetValueAndNotify(ref _propagateCS, value, nameof(_propagateCS));
        }

        public ComputeShader SkyOcclusionCS
        {
            get => _skyOcclusionCS;
            set => this.SetValueAndNotify(ref _skyOcclusionCS, value, nameof(_skyOcclusionCS));
        }

        public int version => _version;

        bool IRenderPipelineGraphicsSettings.isAvailableInPlayerBuild => true;
    }
}