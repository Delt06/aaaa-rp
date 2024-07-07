using System;
using DELTation.AAAARP.Data;
using UnityEngine;
using UnityEngine.Categorization;
using UnityEngine.Rendering;

namespace DELTation.AAAARP
{
    
    [Serializable]
    [SupportedOnRenderPipeline(typeof(AAAARenderPipelineAsset))]
    [CategoryInfo(Name = "R: Runtime Shaders", Order = 1000)]
    public class AAAARenderPipelineRuntimeShaders : IRenderPipelineResources
    {
        [SerializeField] [HideInInspector] private int _version;
        
        [SerializeField]
        [ResourcePath("Shaders/Utils/CoreBlit.shader")]
        private Shader _coreBlitPS;
        
        [SerializeField]
        [ResourcePath("Shaders/Utils/CoreBlitColorAndDepth.shader")]
        private Shader _coreBlitColorAndDepthPS;
        
        public Shader CoreBlitPS
        {
            get => _coreBlitPS;
            set => this.SetValueAndNotify(ref _coreBlitPS, value, nameof(_coreBlitPS));
        }
        
        public Shader CoreBlitColorAndDepthPS
        {
            get => _coreBlitColorAndDepthPS;
            set => this.SetValueAndNotify(ref _coreBlitColorAndDepthPS, value, nameof(_coreBlitColorAndDepthPS));
        }
        
        public int version => _version;
        
        public bool isAvailableInPlayerBuild => true;
    }
}