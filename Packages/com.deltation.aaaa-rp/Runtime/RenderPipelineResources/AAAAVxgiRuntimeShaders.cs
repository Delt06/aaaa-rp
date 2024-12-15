using System;
using DELTation.AAAARP.Data;
using UnityEngine;
using UnityEngine.Categorization;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.RenderPipelineResources
{
    [Serializable]
    [SupportedOnRenderPipeline(typeof(AAAARenderPipelineAsset))]
    [CategoryInfo(Name = "R: Voxel Global Illumination Runtime Shaders", Order = 2000)]
    public class AAAAVxgiRuntimeShaders : IRenderPipelineResources
    {
        private const string BaseResourcePath = "Shaders/GlobalIllumination/VXGI/";

        [SerializeField] [HideInInspector] private int _version;

        [SerializeField]
        [ResourcePath(BaseResourcePath + "Voxelize.shader")]
        private Shader _voxelizePS;
        
        [SerializeField]
        [ResourcePath(BaseResourcePath + "Unpack.compute")]
        private ComputeShader _unpackCS;

        public Shader VoxelizePS
        {
            get => _voxelizePS;
            set => this.SetValueAndNotify(ref _voxelizePS, value, nameof(_voxelizePS));
        }

        public ComputeShader UnpackCS
        {
            get => _unpackCS;
            set => _unpackCS = value;
        }

        public int version => _version;

        bool IRenderPipelineGraphicsSettings.isAvailableInPlayerBuild => true;
    }
}