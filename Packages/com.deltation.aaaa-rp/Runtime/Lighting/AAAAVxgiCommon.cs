namespace DELTation.AAAARP.Lighting
{
    /// <summary>
    ///     Sources:
    ///     - https://wickedengine.net/2017/08/voxel-based-global-illumination/
    /// </summary>
    internal static class AAAAVxgiCommon
    {
        public enum Channels
        {
            BaseColorR = 0,
            BaseColorG,
            BaseColorB,
            BaseColorA,
            EmissiveR,
            EmissiveG,
            EmissiveB,
            DirectLightR,
            DirectLightG,
            DirectLightB,
            PackedNormalR,
            PackedNormalG,
            FragmentCount,
            TotalCount,
        }

        public const string ResourceNamePrefix = "VXGI_";
    }
}