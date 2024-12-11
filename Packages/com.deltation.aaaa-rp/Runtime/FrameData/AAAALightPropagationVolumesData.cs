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
        public NativeList<AAAALightPropagationVolumes.RsmLight> Lights;
        public GridBufferSet PackedGridBuffers;
        public NativeHashMap<int, int> ShadowLightToRSMLightMapping;
        public GridTextureSet UnpackedGridTextures;

        public override void Reset()
        {
            BlockingPotential = default;
            PackedGridBuffers = default;
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

        public struct GridBufferSet
        {
            public BufferDesc SHDesc;
            public BufferHandle RedSH;
            public BufferHandle GreenSH;
            public BufferHandle BlueSH;
            public BufferHandle BlockingPotentialSH;
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