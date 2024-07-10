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
        private TextureHandle _gbufferAlbedo;
        private TextureHandle _gbufferNormals;
        private TextureHandle _visibilityBuffer;
        
        internal ActiveID ActiveColorID { get; set; }
        
        public TextureHandle CameraScaledColorBuffer => CheckAndGetTextureHandle(ref _cameraScaledColorBuffer);
        
        public TextureHandle CameraScaledDepthBuffer => CheckAndGetTextureHandle(ref _cameraScaledDepthBuffer);
        public TextureHandle CameraColorBuffer => CheckAndGetTextureHandle(ref _cameraColorBuffer);
        public TextureHandle CameraDepthBuffer => CheckAndGetTextureHandle(ref _cameraDepthBuffer);
        public TextureHandle CameraResolveColorBuffer => CheckAndGetTextureHandle(ref _cameraResolveColorBuffer);
        public TextureHandle CameraResolveDepthBuffer => CheckAndGetTextureHandle(ref _cameraResolveDepthBuffer);
        
        public TextureHandle VisibilityBuffer => CheckAndGetTextureHandle(ref _visibilityBuffer);
        
        public TextureHandle GBufferAlbedo => CheckAndGetTextureHandle(ref _gbufferAlbedo);
        public TextureHandle GBufferNormals => CheckAndGetTextureHandle(ref _gbufferNormals);
        
        public bool IsActiveTargetBackBuffer
        {
            get
            {
                if (!IsAccessible)
                {
                    Debug.LogError("Trying to access frameData outside of the current frame setup.");
                    return false;
                }
                
                return ActiveColorID == ActiveID.BackBuffer;
            }
        }
        
        public void InitTextures(RenderGraph renderGraph, AAAACameraData cameraData)
        {
            ActiveColorID = ActiveID.Camera;
            
            CreateTargets(renderGraph, cameraData);
            ImportFinalTarget(renderGraph, cameraData);
        }
        
        private void CreateTargets(RenderGraph renderGraph, AAAACameraData cameraData)
        {
            var scaledCameraTargetViewportSize = new int2(cameraData.ScaledWidth, cameraData.ScaledHeight);
            
            {
                TextureDesc cameraColorDesc = AAAARenderingUtils.CreateTextureDesc(null, cameraData.CameraTargetDescriptor);
                cameraColorDesc.depthBufferBits = DepthBits.None;
                cameraColorDesc.filterMode = FilterMode.Bilinear;
                cameraColorDesc.wrapMode = TextureWrapMode.Clamp;
                cameraColorDesc.clearBuffer = cameraData.ClearColor;
                cameraColorDesc.clearColor = cameraData.BackgroundColor;
                
                TextureDesc cameraDepthDesc = AAAARenderingUtils.CreateTextureDesc(null, cameraData.CameraTargetDescriptor);
                cameraDepthDesc.colorFormat = GraphicsFormat.D24_UNorm_S8_UInt;
                cameraDepthDesc.depthBufferBits = DepthBits.Depth32;
                cameraDepthDesc.filterMode = FilterMode.Point;
                cameraDepthDesc.wrapMode = TextureWrapMode.Clamp;
                cameraDepthDesc.clearBuffer = cameraData.ClearDepth;
                
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
                
                _cameraColorBuffer = _cameraScaledColorBuffer;
                _cameraDepthBuffer = _cameraScaledDepthBuffer;
            }
            
            {
                TextureDesc desc = AAAARenderingUtils.CreateTextureDesc("VisibilityBuffer", cameraData.CameraTargetDescriptor);
                desc.colorFormat = GraphicsFormat.R32G32_UInt;
                desc.depthBufferBits = DepthBits.None;
                desc.filterMode = FilterMode.Point;
                desc.wrapMode = TextureWrapMode.Clamp;
                desc.clearBuffer = true;
                desc.clearColor = new Color(-1, -1, 0.0f, 0.0f);
                desc.width = scaledCameraTargetViewportSize.x;
                desc.height = scaledCameraTargetViewportSize.y;
                
                _visibilityBuffer = renderGraph.CreateTexture(desc);
            }
            
            {
                TextureDesc desc = AAAARenderingUtils.CreateTextureDesc("GBuffer_Albedo", cameraData.CameraTargetDescriptor);
                desc.depthBufferBits = DepthBits.None;
                desc.colorFormat = GraphicsFormat.R8G8B8A8_UNorm;
                desc.filterMode = FilterMode.Bilinear;
                desc.wrapMode = TextureWrapMode.Clamp;
                desc.clearBuffer = true;
                desc.clearColor = Color.clear;
                desc.width = scaledCameraTargetViewportSize.x;
                desc.height = scaledCameraTargetViewportSize.y;
                
                _gbufferAlbedo = renderGraph.CreateTexture(desc);
            }
            
            {
                TextureDesc desc = AAAARenderingUtils.CreateTextureDesc("GBuffer_Normals", cameraData.CameraTargetDescriptor);
                desc.depthBufferBits = DepthBits.None;
                desc.colorFormat = GraphicsFormat.R8G8B8A8_UNorm;
                desc.filterMode = FilterMode.Bilinear;
                desc.wrapMode = TextureWrapMode.Clamp;
                desc.clearBuffer = true;
                desc.clearColor = Color.clear;
                desc.width = scaledCameraTargetViewportSize.x;
                desc.height = scaledCameraTargetViewportSize.y;
                
                _gbufferNormals = renderGraph.CreateTexture(desc);
            }
        }
        
        private void ImportFinalTarget(RenderGraph renderGraph, AAAACameraData cameraData)
        {
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
            _cameraResolveColorBuffer = finalTarget;
            _cameraResolveDepthBuffer = finalTargetDepth;
        }
        
        public override void Reset()
        {
            _cameraScaledColorBuffer = default;
            _cameraScaledDepthBuffer = default;
            _cameraColorBuffer = default;
            _cameraDepthBuffer = default;
            _cameraResolveColorBuffer = default;
            _cameraResolveDepthBuffer = default;
            
            _visibilityBuffer = default;
            
            _gbufferAlbedo = default;
            _gbufferNormals = default;
        }
    }
}