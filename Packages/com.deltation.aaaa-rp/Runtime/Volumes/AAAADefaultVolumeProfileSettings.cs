using System;
using DELTation.AAAARP.Data;
using UnityEngine;
using UnityEngine.Categorization;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Volumes
{
    [Serializable]
    [SupportedOnRenderPipeline(typeof(AAAARenderPipelineAsset))]
    [CategoryInfo(Name = "Volume", Order = 0)]
    public class AAAADefaultVolumeProfileSettings : IDefaultVolumeProfileSettings
    {
        [SerializeField] [HideInInspector] private Version _version;
        [SerializeField] private VolumeProfile _volumeProfile;

        public int version => (int) _version;

        public VolumeProfile volumeProfile
        {
            get => _volumeProfile;
            set => this.SetValueAndNotify(ref _volumeProfile, value);
        }

        internal enum Version
        {
            Initial = 0,
        }
    }
}