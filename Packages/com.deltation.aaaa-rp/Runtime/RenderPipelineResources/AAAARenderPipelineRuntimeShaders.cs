using System;
using DELTation.AAAARP.Data;
using UnityEngine;
using UnityEngine.Categorization;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.RenderPipelineResources
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
        [ResourcePath("Shaders/IBL/ConvolveDiffuseIrradiance.shader")]
        private Shader _convolveDiffuseIrradiancePS;

        [SerializeField]
        [ResourcePath("Shaders/IBL/BRDFIntegration.shader")]
        private Shader _brdfIntegrationPS;

        [SerializeField]
        [ResourcePath("Shaders/IBL/PreFilterEnvironment.shader")]
        private Shader _preFilterEnvironmentPS;

        [SerializeField]
        [ResourcePath("Shaders/VisibilityBuffer/VisibilityBuffer.shader")]
        private Shader _visibilityBufferPS;

        [SerializeField]
        [ResourcePath("Shaders/VisibilityBuffer/VisibilityBufferResolve.shader")]
        private Shader _visibilityBufferResolvePS;

        [SerializeField]
        [ResourcePath("Shaders/VisibilityBuffer/GPUInstanceCulling.compute")]
        private ComputeShader _gpuInstanceCullingCS;

        [SerializeField]
        [ResourcePath("Shaders/VisibilityBuffer/MeshletListBuild.compute")]
        private ComputeShader _meshletListBuildCS;

        [SerializeField]
        [ResourcePath("Shaders/VisibilityBuffer/FixupGPUMeshletCullingIndirectDispatchArgs.compute")]
        private ComputeShader _fixupGPUMeshletCullingIndirectDispatchArgsCS;

        [SerializeField]
        [ResourcePath("Shaders/VisibilityBuffer/GPUMeshletCulling.compute")]
        private ComputeShader _gpuMeshletCullingCS;

        [SerializeField]
        [ResourcePath("Shaders/ClusteredLighting/BuildClusterGrid.compute")]
        private ComputeShader _buildClusterGridCS;

        [SerializeField]
        [ResourcePath("Shaders/ClusteredLighting/ClusterCulling.compute")]
        private ComputeShader _clusterCullingCS;

        [SerializeField]
        [ResourcePath("Shaders/Lighting/DeferredLighting.shader")]
        private Shader _deferredLightingPS;

        [SerializeField]
        [ResourcePath("Shaders/Lighting/DeferredReflections.shader")]
        private Shader _deferredReflectionsPS;

        [SerializeField]
        [ResourcePath("Shaders/VisibilityBuffer/HZBGeneration.compute")]
        private ComputeShader _hzbGenerationCS;

        [SerializeField]
        [ResourcePath("Shaders/GlobalIllumination/SSRTrace.compute")]
        private ComputeShader _ssrTraceCS;

        [SerializeField]
        [ResourcePath("Shaders/GlobalIllumination/SSRResolve.shader")]
        private Shader _ssrResolvePS;

        [SerializeField]
        [ResourcePath("Shaders/GlobalIllumination/LPVTrace.shader")]
        private Shader _lpvTracePS;

        [SerializeField]
        [ResourcePath("Shaders/AntiAliasing/SMAA.shader")]
        private Shader _smaaPS;

        [SerializeField]
        [ResourcePath("Shaders/PostProcessing/Uber.shader")]
        private Shader _uberPostProcessingPS;

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

        public Shader ConvolveDiffuseIrradiancePS
        {
            get => _convolveDiffuseIrradiancePS;
            set => this.SetValueAndNotify(ref _convolveDiffuseIrradiancePS, value, nameof(_convolveDiffuseIrradiancePS));
        }

        public Shader BRDFIntegrationPS
        {
            get => _brdfIntegrationPS;
            set => this.SetValueAndNotify(ref _brdfIntegrationPS, value, nameof(_brdfIntegrationPS));
        }

        public Shader PreFilterEnvironmentPS
        {
            get => _preFilterEnvironmentPS;
            set => this.SetValueAndNotify(ref _preFilterEnvironmentPS, value, nameof(_preFilterEnvironmentPS));
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

        public ComputeShader GPUInstanceCullingCS
        {
            get => _gpuInstanceCullingCS;
            set => this.SetValueAndNotify(ref _gpuInstanceCullingCS, value, nameof(_gpuInstanceCullingCS));
        }

        public ComputeShader MeshletListBuildCS
        {
            get => _meshletListBuildCS;
            set => this.SetValueAndNotify(ref _meshletListBuildCS, value, nameof(_meshletListBuildCS));
        }

        public ComputeShader FixupGPUMeshletCullingIndirectDispatchArgsCS
        {
            get => _fixupGPUMeshletCullingIndirectDispatchArgsCS;
            set => this.SetValueAndNotify(ref _fixupGPUMeshletCullingIndirectDispatchArgsCS, value, nameof(_fixupGPUMeshletCullingIndirectDispatchArgsCS));
        }

        public ComputeShader GPUMeshletCullingCS
        {
            get => _gpuMeshletCullingCS;
            set => this.SetValueAndNotify(ref _gpuMeshletCullingCS, value, nameof(_gpuMeshletCullingCS));
        }

        public ComputeShader HZBGenerationCS
        {
            get => _hzbGenerationCS;
            set => this.SetValueAndNotify(ref _hzbGenerationCS, value, nameof(_hzbGenerationCS));
        }

        public ComputeShader BuildClusterGridCS
        {
            get => _buildClusterGridCS;
            set => this.SetValueAndNotify(ref _buildClusterGridCS, value, nameof(_buildClusterGridCS));
        }

        public ComputeShader ClusterCullingCS
        {
            get => _clusterCullingCS;
            set => this.SetValueAndNotify(ref _clusterCullingCS, value, nameof(_clusterCullingCS));
        }

        public Shader DeferredLightingPS
        {
            get => _deferredLightingPS;
            set => this.SetValueAndNotify(ref _deferredLightingPS, value, nameof(_deferredLightingPS));
        }

        public Shader DeferredReflectionsPS
        {
            get => _deferredReflectionsPS;
            set => this.SetValueAndNotify(ref _deferredReflectionsPS, value, nameof(_deferredReflectionsPS));
        }

        public ComputeShader SsrTraceCS
        {
            get => _ssrTraceCS;
            set => this.SetValueAndNotify(ref _ssrTraceCS, value, nameof(_ssrTraceCS));
        }

        public Shader SsrResolvePS
        {
            get => _ssrResolvePS;
            set => this.SetValueAndNotify(ref _ssrResolvePS, value, nameof(_ssrResolvePS));
        }

        public Shader LpvTracePS
        {
            get => _lpvTracePS;
            set => this.SetValueAndNotify(ref _lpvTracePS, value, nameof(_lpvTracePS));
        }

        public Shader SmaaPS
        {
            get => _smaaPS;
            set => this.SetValueAndNotify(ref _smaaPS, value, nameof(_smaaPS));
        }

        public Shader UberPostProcessingPS
        {
            get => _uberPostProcessingPS;
            set => this.SetValueAndNotify(ref _uberPostProcessingPS, value, nameof(_uberPostProcessingPS));
        }

        public int version => _version;

        bool IRenderPipelineGraphicsSettings.isAvailableInPlayerBuild => true;
    }
}