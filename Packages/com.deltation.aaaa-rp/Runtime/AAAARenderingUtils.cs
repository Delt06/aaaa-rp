using DELTation.AAAARP.FrameData;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP
{
    public static class AAAARenderingUtils
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
        
        internal static Vector4 GetFinalBlitScaleBias(RTHandle source, RTHandle destination, AAAACameraData cameraData)
        {
            Vector2 viewportScale = source.useScaling
                ? new Vector2(source.rtHandleProperties.rtHandleScale.x, source.rtHandleProperties.rtHandleScale.y)
                : Vector2.one;
            bool yFlip = cameraData.IsRenderTargetProjectionMatrixFlipped(destination);
            Vector4 scaleBias =
                !yFlip ? new Vector4(viewportScale.x, -viewportScale.y, 0, viewportScale.y) : new Vector4(viewportScale.x, viewportScale.y, 0, 0);
            
            return scaleBias;
        }
    }
}