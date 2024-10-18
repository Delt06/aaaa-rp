using System;

namespace DELTation.AAAARP.Editor.AssetPostProcessors
{
    [Serializable]
    public class AAAAModelSettings
    {
        public bool GenerateMaterialAssets = true;
        public bool CleanupDefaultMaterials = true;
        public bool CleanupDefaultMeshes = true;
    }
}