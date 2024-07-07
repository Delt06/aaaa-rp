using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.FrameData
{
    public class AAAAResourceData : AAAAResourceDataBase
    {
        private TextureHandle _cameraColorBuffer;
        private TextureHandle _cameraDepthBuffer;
        private TextureHandle _cameraResolveColorBuffer;
        private TextureHandle _cameraResolveDepthBuffer;
        private TextureHandle _cameraScaledColorBuffer;
        private TextureHandle _cameraScaledDepthBuffer;
        
        public TextureHandle CameraScaledColorBuffer
        {
            get => CheckAndGetTextureHandle(ref _cameraScaledColorBuffer);
            set => CheckAndSetTextureHandle(ref _cameraScaledColorBuffer, value);
        }
        
        public TextureHandle CameraScaledDepthBuffer
        {
            get => CheckAndGetTextureHandle(ref _cameraScaledDepthBuffer);
            set => CheckAndSetTextureHandle(ref _cameraScaledDepthBuffer, value);
        }
        public TextureHandle CameraColorBuffer
        {
            get => CheckAndGetTextureHandle(ref _cameraColorBuffer);
            set => CheckAndSetTextureHandle(ref _cameraColorBuffer, value);
        }
        public TextureHandle CameraDepthBuffer
        {
            get => CheckAndGetTextureHandle(ref _cameraDepthBuffer);
            set => CheckAndSetTextureHandle(ref _cameraDepthBuffer, value);
        }
        public TextureHandle CameraResolveColorBuffer
        {
            get => CheckAndGetTextureHandle(ref _cameraResolveColorBuffer);
            set => CheckAndSetTextureHandle(ref _cameraResolveColorBuffer, value);
        }
        public TextureHandle CameraResolveDepthBuffer
        {
            get => CheckAndGetTextureHandle(ref _cameraResolveDepthBuffer);
            set => CheckAndSetTextureHandle(ref _cameraResolveDepthBuffer, value);
        }
        
        public void InitTextures(RenderGraph renderGraph, AAAACameraData cameraData)
        {
            TextureDesc cameraColorDesc = AAAARenderingUtils.CreateTextureDesc(null, cameraData.CameraTargetDescriptor);
            cameraColorDesc.depthBufferBits = DepthBits.None;
            cameraColorDesc.filterMode = FilterMode.Bilinear;
            cameraColorDesc.wrapMode = TextureWrapMode.Clamp;
            cameraColorDesc.enableRandomWrite = true;
            
            TextureDesc cameraDepthDesc = AAAARenderingUtils.CreateTextureDesc(null, cameraData.CameraTargetDescriptor);
            cameraDepthDesc.colorFormat = GraphicsFormat.D24_UNorm_S8_UInt;
            cameraDepthDesc.depthBufferBits = DepthBits.Depth32;
            cameraDepthDesc.filterMode = FilterMode.Point;
            cameraDepthDesc.wrapMode = TextureWrapMode.Clamp;
            
            var scaledCameraTargetViewportSize = new int2(cameraData.ScaledWidth, cameraData.ScaledHeight);
            
            // Scaled color
            cameraColorDesc.name = "CameraColor_Scaled";
            cameraColorDesc.width = scaledCameraTargetViewportSize.x;
            cameraColorDesc.height = scaledCameraTargetViewportSize.y;
            _cameraScaledColorBuffer = renderGraph.CreateTexture(cameraColorDesc);
            
            // Scaled depth
            cameraDepthDesc.name = "CameraDepth_Scaled";
            cameraDepthDesc.width = scaledCameraTargetViewportSize.x;
            cameraDepthDesc.height = scaledCameraTargetViewportSize.y;
            _cameraScaledDepthBuffer = renderGraph.CreateTexture(cameraDepthDesc);
            
            //BuiltinRenderTextureType.CameraTarget so this is either system render target or camera.targetTexture if non null
            //NOTE: Careful what you use here as many of the properties bake-in the camera rect so for example
            //cameraData.cameraTargetDescriptor.width is the width of the rectangle but not the actual render target
            //same with cameraData.camera.pixelWidth
            // For BuiltinRenderTextureType wrapping RTHandles RenderGraph can't know what they are so we have to pass it in.
            var importInfo = new RenderTargetInfo
            {
                width = Screen.width,
                height = Screen.height,
                volumeDepth = 1,
                msaaSamples = 1,
                format = AAAARenderPipelineCore.MakeRenderTextureGraphicsFormat(cameraData.IsHdrEnabled, cameraData.HDRColorBufferPrecision,
                    Graphics.preserveFramebufferAlpha
                ),
            };
            
            RenderTargetInfo importInfoDepth = importInfo;
            importInfoDepth.format = SystemInfo.GetGraphicsFormat(DefaultFormat.DepthStencil);
            
            // final target (color buffer)
            if (cameraData.TargetTexture != null)
            {
                importInfo.width = cameraData.TargetTexture.width;
                importInfo.height = cameraData.TargetTexture.height;
                importInfo.format = cameraData.TargetTexture.graphicsFormat;
                
                importInfoDepth.width = cameraData.TargetTexture.width;
                importInfoDepth.height = cameraData.TargetTexture.height;
                importInfoDepth.format = cameraData.TargetTexture.depthStencilFormat;
                
                // We let users know that a depth format is required for correct usage, but we fallback to the old default depth format behaviour to avoid regressions
                if (importInfoDepth.format == GraphicsFormat.None)
                {
                    importInfoDepth.format = SystemInfo.GetGraphicsFormat(DefaultFormat.DepthStencil);
                    Debug.LogWarning(
                        $"In the render graph API, the output Render Texture must have a depth buffer. When you select a Render Texture in any camera's Output Texture property, the Depth Stencil Format property of the texture must be set to a value other than None. Camera: {cameraData.Camera.name} Texture: {cameraData.TargetTexture.name}"
                    );
                }
            }
            
            TextureHandle finalTarget = renderGraph.ImportTexture(cameraData.Renderer.CurrentColorBuffer, importInfo);
            TextureHandle finalTargetDepth = renderGraph.ImportTexture(cameraData.Renderer.CurrentDepthBuffer, importInfoDepth);
            
            _cameraColorBuffer = _cameraScaledColorBuffer;
            _cameraDepthBuffer = _cameraScaledDepthBuffer;
            
            _cameraResolveColorBuffer = finalTarget;
            _cameraResolveDepthBuffer = finalTargetDepth;
        }
        
        public override void Reset()
        {
            _cameraScaledColorBuffer = default;
            _cameraScaledDepthBuffer = default;
        }
    }
}