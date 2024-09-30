using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Meshlets;
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
        private TextureHandle _cameraHzbScaled;
        private TextureHandle _cameraResolveColorBuffer;
        private TextureHandle _cameraResolveDepthBuffer;
        private TextureHandle _cameraScaledColorBuffer;
        private TextureHandle _cameraScaledDepthBuffer;
        private TextureHandle _gbufferAlbedo;
        private TextureHandle _gbufferMasks;
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
        public TextureHandle GBufferMasks => CheckAndGetTextureHandle(ref _gbufferMasks);

        public TextureHandle CameraHZBScaled => CheckAndGetTextureHandle(ref _cameraHzbScaled);

        public HZBInfo CameraScaledHZBInfo { get; } = new();

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

        public TextureDesc CameraScaledColorDesc { get; private set; }
        public TextureDesc CameraColorDesc { get; private set; }

        public void InitTextures(RenderGraph renderGraph, AAAARenderingData renderingData, AAAACameraData cameraData)
        {
            ActiveColorID = ActiveID.Camera;

            CreateTargets(renderGraph, renderingData, cameraData);
            ImportFinalTarget(renderGraph, cameraData);
        }

        private void CreateTargets(RenderGraph renderGraph, AAAARenderingData renderingData, AAAACameraData cameraData)
        {
            var scaledCameraTargetViewportSize = new int2(cameraData.ScaledWidth, cameraData.ScaledHeight);

            {
                TextureDesc cameraColorDesc = AAAARenderingUtils.CreateTextureDesc(null, cameraData.CameraTargetDescriptor);
                cameraColorDesc.name = "CameraColor";
                cameraColorDesc.depthBufferBits = DepthBits.None;
                cameraColorDesc.filterMode = FilterMode.Bilinear;
                cameraColorDesc.wrapMode = TextureWrapMode.Clamp;
                cameraColorDesc.clearBuffer = cameraData.ClearColor;
                cameraColorDesc.clearColor = cameraData.BackgroundColor;
                CameraColorDesc = cameraColorDesc;
                _cameraColorBuffer = renderGraph.CreateTexture(cameraColorDesc);

                TextureDesc cameraDepthDesc = AAAARenderingUtils.CreateTextureDesc(null, cameraData.CameraTargetDescriptor);
                cameraColorDesc.name = "CameraDepth";
                cameraDepthDesc.colorFormat = GraphicsFormat.D24_UNorm_S8_UInt;
                cameraDepthDesc.depthBufferBits = DepthBits.Depth32;
                cameraDepthDesc.filterMode = FilterMode.Point;
                cameraDepthDesc.wrapMode = TextureWrapMode.Clamp;
                cameraDepthDesc.clearBuffer = cameraData.ClearDepth;
                _cameraDepthBuffer = renderGraph.CreateTexture(cameraDepthDesc);

                // Scaled color
                cameraColorDesc.name = "CameraColor_Scaled";
                cameraColorDesc.width = scaledCameraTargetViewportSize.x;
                cameraColorDesc.height = scaledCameraTargetViewportSize.y;
                cameraColorDesc.clearBuffer = true;
                cameraColorDesc.clearColor = Color.clear;
                CameraScaledColorDesc = cameraColorDesc;
                _cameraScaledColorBuffer = renderGraph.CreateTexture(cameraColorDesc);

                // Scaled depth
                cameraDepthDesc.name = "CameraDepth_Scaled";
                cameraDepthDesc.width = scaledCameraTargetViewportSize.x;
                cameraDepthDesc.height = scaledCameraTargetViewportSize.y;
                _cameraScaledDepthBuffer = renderGraph.CreateTexture(cameraDepthDesc);

                CameraScaledHZBInfo.Compute(new int2(cameraDepthDesc.width, cameraDepthDesc.height));

                TextureDesc cameraHzbScaledDesc = cameraDepthDesc;
                cameraHzbScaledDesc.width = CameraScaledHZBInfo.TextureSize.x;
                cameraHzbScaledDesc.height = CameraScaledHZBInfo.TextureSize.y;
                cameraHzbScaledDesc.colorFormat = GraphicsFormat.R32_SFloat;
                cameraHzbScaledDesc.depthBufferBits = 0;
                cameraHzbScaledDesc.enableRandomWrite = true;
                cameraHzbScaledDesc.name = "CameraHZB_Scaled";
                cameraHzbScaledDesc.clearBuffer = false;
                _cameraHzbScaled = renderGraph.CreateTexture(cameraHzbScaledDesc);
            }

            {
                TextureDesc desc = AAAARenderingUtils.CreateTextureDesc("VisibilityBuffer", cameraData.CameraTargetDescriptor);
                desc.colorFormat = GraphicsFormat.R32G32_UInt;
                desc.depthBufferBits = DepthBits.None;
                desc.filterMode = FilterMode.Point;
                desc.wrapMode = TextureWrapMode.Clamp;
                desc.clearBuffer = true;
                desc.clearColor = new Color(float.PositiveInfinity, float.PositiveInfinity, 0.0f, 0.0f);
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
                desc.colorFormat = GraphicsFormat.R16G16_SNorm;
                desc.filterMode = FilterMode.Bilinear;
                desc.wrapMode = TextureWrapMode.Clamp;
                desc.clearBuffer = true;
                desc.clearColor = Color.clear;
                desc.width = scaledCameraTargetViewportSize.x;
                desc.height = scaledCameraTargetViewportSize.y;

                _gbufferNormals = renderGraph.CreateTexture(desc);
            }

            {
                TextureDesc desc = AAAARenderingUtils.CreateTextureDesc("GBuffer_Masks", cameraData.CameraTargetDescriptor);
                desc.depthBufferBits = DepthBits.None;
                desc.colorFormat = GraphicsFormat.R8G8B8A8_UNorm;
                desc.filterMode = FilterMode.Bilinear;
                desc.wrapMode = TextureWrapMode.Clamp;
                desc.clearBuffer = true;
                desc.clearColor = Color.clear;
                desc.width = scaledCameraTargetViewportSize.x;
                desc.height = scaledCameraTargetViewportSize.y;

                _gbufferMasks = renderGraph.CreateTexture(desc);
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
            CameraColorDesc = default;
            _cameraColorBuffer = default;
            _cameraDepthBuffer = default;

            CameraScaledColorDesc = default;
            _cameraScaledColorBuffer = default;
            _cameraScaledDepthBuffer = default;

            _cameraResolveColorBuffer = default;
            _cameraResolveDepthBuffer = default;

            _visibilityBuffer = default;

            _gbufferAlbedo = default;
            _gbufferNormals = default;
            _gbufferMasks = default;

            _cameraHzbScaled = default;
        }

        public class HZBInfo
        {
            public readonly Vector4[] MipRects = new Vector4[AAAAMeshletComputeShaders.HZBMaxLevelCount];
            public int LevelCount { get; private set; }
            public int2 TextureSize { get; private set; }

            // https://github.com/Unity-Technologies/Graphics/blob/3fb000debc138e82dcd7ac069c6818c4857a78da/Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/Utility/HDUtils.cs#L669
            // We pack all MIP levels into the top MIP level to avoid the Pow2 MIP chain restriction.
            // We compute the required size iteratively.
            // This function is NOT fast, but it is illustrative, and can be optimized later.
            public void Compute(int2 viewportSize)
            {
                TextureSize = viewportSize >> 1;
                MipRects[0] = new Vector4(0, 0, viewportSize.x, viewportSize.y);

                int mipLevel = 0;
                int2 mipSize = viewportSize;

                do
                {
                    mipLevel++;

                    // Round up.
                    mipSize.x = math.max(1, mipSize.x + 1 >> 1);
                    mipSize.y = math.max(1, mipSize.y + 1 >> 1);

                    float4 prevRect = MipRects[mipLevel - 1];

                    var prevMipBegin = (int2) prevRect.xy;
                    int2 prevMipEnd = prevMipBegin + (int2) prevRect.zw;

                    int2 mipBegin = 0;

                    if (mipLevel > 1)
                    {
                        if ((mipLevel & 1) != 0) // Odd
                        {
                            mipBegin.x = prevMipBegin.x;
                            mipBegin.y = prevMipEnd.y;
                        }
                        else // Even
                        {
                            mipBegin.x = prevMipEnd.x;
                            mipBegin.y = prevMipBegin.y;
                        }
                    }

                    MipRects[mipLevel] = new Vector4(mipBegin.x, mipBegin.y, mipSize.x, mipSize.y);
                    TextureSize = math.max(TextureSize, mipBegin + mipSize);

                } while (mipSize.x > 1 || mipSize.y > 1);

                LevelCount = mipLevel + 1;
            }
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public static class ShaderPropertyID
        {
            public static readonly int _VisibilityBuffer = Shader.PropertyToID(nameof(_VisibilityBuffer));

            public static readonly int _GBuffer_Albedo = Shader.PropertyToID(nameof(_GBuffer_Albedo));
            public static readonly int _GBuffer_Normals = Shader.PropertyToID(nameof(_GBuffer_Normals));
            public static readonly int _GBuffer_Masks = Shader.PropertyToID(nameof(_GBuffer_Masks));

            public static readonly int _CameraDepth = Shader.PropertyToID(nameof(_CameraDepth));
        }
    }
}