﻿using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.FrameData
{
    public class AAAAVoxelGlobalIlluminationData : ContextItem
    {
        public int GridMipCount;
        public TextureHandle GridNormals;
        public TextureHandle GridRadiance;
        public int GridSize;
        public TextureHandle IndirectDiffuseTexture;
        public TextureHandle IndirectSpecularTexture;
        public BufferHandle PackedGridBuffer;
        public BufferDesc PackedGridBufferDesc;
        public int RenderScale;

        public override void Reset()
        {
            GridSize = 0;
            PackedGridBufferDesc = default;
            PackedGridBuffer = default;
            GridRadiance = default;
            GridNormals = default;
            GridMipCount = 0;
            IndirectDiffuseTexture = default;
            IndirectSpecularTexture = default;
            RenderScale = 0;
        }
    }
}