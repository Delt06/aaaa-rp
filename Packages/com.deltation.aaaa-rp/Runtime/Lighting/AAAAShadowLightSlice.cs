using Unity.Mathematics;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Lighting
{
    [GenerateHLSL(PackingRules.Exact, needAccessors = false)]
    public struct AAAAShadowLightSlice
    {
        public float4x4 WorldToShadowCoords;
        public float4 BoundingSphere;
        public float4 AtlasSize;
        public int BindlessShadowMapIndex;
        public int Padding0;
        public int Padding1;
        public int Padding2;
    }
}