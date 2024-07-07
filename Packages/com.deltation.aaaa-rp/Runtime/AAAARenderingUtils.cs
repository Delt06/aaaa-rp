using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP
{
    public class AAAARenderingUtils
    {
        public static TextureDesc CreateTextureDesc(string name, RenderTextureDescriptor input) =>
            new(input.width, input.height)
            {
                colorFormat = input.graphicsFormat,
                depthBufferBits = (DepthBits) input.depthBufferBits,
                dimension = input.dimension,
                slices = input.volumeDepth,
                name = name,
            };
    }
}