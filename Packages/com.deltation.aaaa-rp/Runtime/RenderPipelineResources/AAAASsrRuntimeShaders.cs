using System;
using DELTation.AAAARP.Data;
using UnityEngine;
using UnityEngine.Categorization;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.RenderPipelineResources
{
    [Serializable]
    [SupportedOnRenderPipeline(typeof(AAAARenderPipelineAsset))]
    [CategoryInfo(Name = "R: Scree-Space Reflections Runtime Shaders", Order = 2000)]
    public class AAAASsrRuntimeShaders : IRenderPipelineResources
    {
        private const string BaseResourcePath = "Shaders/GlobalIllumination/SSR/";

        [SerializeField] [HideInInspector] private int _version;

        [SerializeField]
        [ResourcePath(BaseResourcePath + "SSRTrace.compute")]
        private ComputeShader _traceCS;

        [SerializeField]
        [ResourcePath(BaseResourcePath + "SSRResolve.shader")]
        private Shader _resolvePS;

        public ComputeShader TraceCS
        {
            get => _traceCS;
            set => this.SetValueAndNotify(ref _traceCS, value, nameof(_traceCS));
        }

        public Shader ResolvePS
        {
            get => _resolvePS;
            set => this.SetValueAndNotify(ref _resolvePS, value, nameof(_resolvePS));
        }

        public int version => _version;

        bool IRenderPipelineGraphicsSettings.isAvailableInPlayerBuild => true;
    }
}