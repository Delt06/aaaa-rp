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

        [SerializeField]
        [ResourcePath("Shaders/Utils/RawBufferClear.compute")]
        private ComputeShader _rawBufferClearCS;

        [SerializeField]
        [ResourcePath("Shaders/VisibilityBuffer/VisibilityBuffer.shader")]
        private Shader _visibilityBufferPS;

        [SerializeField]
        [ResourcePath("Shaders/VisibilityBuffer/VisibilityBufferResolve.shader")]
        private Shader _visibilityBufferResolvePS;

        [SerializeField]
        [ResourcePath("Shaders/VisibilityBuffer/GPUMeshletCulling.compute")]
        private ComputeShader _gpuMeshletCullingCS;

        [SerializeField]
        [ResourcePath("Shaders/VisibilityBuffer/FixupMeshletIndirectDrawArgs.compute")]
        private ComputeShader _fixupMeshletIndirectDrawArgsCS;

        [SerializeField]
        [ResourcePath("Shaders/DeferredLighting.shader")]
        private Shader _deferredLightingPS;

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

        public ComputeShader RawBufferClearCS
        {
            get => _rawBufferClearCS;
            set => this.SetValueAndNotify(ref _rawBufferClearCS, value, nameof(_rawBufferClearCS));
        }

        public Shader VisibilityBufferPS
        {
            get => _visibilityBufferPS;
            set => this.SetValueAndNotify(ref _visibilityBufferPS, value, nameof(_visibilityBufferPS));
        }

        public Shader VisibilityBufferResolvePS
        {
            get => _visibilityBufferResolvePS;
            set => this.SetValueAndNotify(ref _visibilityBufferResolvePS, value, nameof(_visibilityBufferResolvePS));
        }

        public ComputeShader GPUMeshletCullingCS
        {
            get => _gpuMeshletCullingCS;
            set => this.SetValueAndNotify(ref _gpuMeshletCullingCS, value, nameof(_gpuMeshletCullingCS));
        }

        public ComputeShader FixupMeshletIndirectDrawArgsCS
        {
            get => _fixupMeshletIndirectDrawArgsCS;
            set => this.SetValueAndNotify(ref _fixupMeshletIndirectDrawArgsCS, value, nameof(_fixupMeshletIndirectDrawArgsCS));
        }

        public Shader DeferredLightingPS
        {
            get => _deferredLightingPS;
            set => this.SetValueAndNotify(ref _deferredLightingPS, value, nameof(_deferredLightingPS));
        }

        public int version => _version;

        bool IRenderPipelineGraphicsSettings.isAvailableInPlayerBuild => true;
    }
}