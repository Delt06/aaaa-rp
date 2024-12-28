using Unity.Mathematics;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.FrameData
{
    public class AAAAVoxelGlobalIlluminationData : ContextItem
    {
        public float3 BoundsMax;
        public float3 BoundsMin;
        public TextureHandle GridAlbedo;
        public TextureHandle GridDirectLighting;
        public TextureHandle GridEmission;
        public TextureHandle GridNormals;
        public int GridSize;
        public BufferHandle PackedGridBuffer;
        public BufferDesc PackedGridBufferDesc;
        public int GridMipCount;

        public override void Reset()
        {
            GridSize = 0;
            BoundsMin = default;
            BoundsMax = default;
            PackedGridBufferDesc = default;
            PackedGridBuffer = default;
            GridAlbedo = default;
            GridEmission = default;
            GridDirectLighting = default;
            GridNormals = default;
            GridMipCount = 0;
        }
    }
}