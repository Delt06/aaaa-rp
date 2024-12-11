using DELTation.AAAARP.Lighting;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.FrameData
{
    public class AAAALightPropagationVolumesData : ContextItem
    {
        public bool BlockingPotential;
        public float3 GridBoundsMax;
        public float3 GridBoundsMin;
        public int GridSize;
        public NativeList<AAAALpvCommon.RsmLight> Lights;
        public GridTextureSet PackedGridTextures;
        public NativeHashMap<int, int> ShadowLightToRSMLightMapping;
        public GridTextureSet UnpackedGridTextures;

        public override void Reset()
        {
            BlockingPotential = default;
            PackedGridTextures = default;
            UnpackedGridTextures = default;
            GridBoundsMin = default;
            GridBoundsMax = default;
            GridSize = default;

            if (Lights.IsCreated)
            {
                Lights.Dispose();
                Lights = default;
            }

            if (ShadowLightToRSMLightMapping.IsCreated)
            {
                ShadowLightToRSMLightMapping.Dispose();
                ShadowLightToRSMLightMapping = default;
            }
        }

        public struct GridTextureSet
        {
            public TextureDesc SHDesc;
            public TextureHandle RedSH;
            public TextureHandle GreenSH;
            public TextureHandle BlueSH;
            public TextureHandle BlockingPotentialSH;
        }
    }
}