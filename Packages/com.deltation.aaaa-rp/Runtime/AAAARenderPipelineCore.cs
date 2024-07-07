using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.FrameData;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace DELTation.AAAARP
{
    internal static class AAAARenderPipelineCore
    {
        internal static GraphicsFormat MakeRenderTextureGraphicsFormat(bool isHdrEnabled, HDRColorBufferPrecision requestHDRColorBufferPrecision,
            bool needsAlpha)
        {
            if (isHdrEnabled)
            {
                if (!needsAlpha && requestHDRColorBufferPrecision != HDRColorBufferPrecision._64Bits &&
                    SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, GraphicsFormatUsage.Blend))
                    return GraphicsFormat.B10G11R11_UFloatPack32;
                if (SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.Blend))
                    return GraphicsFormat.R16G16B16A16_SFloat;
                return SystemInfo.GetGraphicsFormat(DefaultFormat.HDR); // This might actually be a LDR format on old devices.
            }
            
            return SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
        }
        
        internal static RenderTextureDescriptor CreateRenderTextureDescriptor(Camera camera, AAAACameraData cameraData,
            bool isHdrEnabled, HDRColorBufferPrecision requestHDRColorBufferPrecision, bool needsAlpha)
        {
            RenderTextureDescriptor desc;
            const int msaaSamples = 1;
            
            if (camera.targetTexture == null)
            {
                desc = new RenderTextureDescriptor(cameraData.ScaledWidth, cameraData.ScaledHeight)
                {
                    graphicsFormat = MakeRenderTextureGraphicsFormat(isHdrEnabled, requestHDRColorBufferPrecision, needsAlpha),
                    depthBufferBits = 32,
                    msaaSamples = msaaSamples,
                    sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear,
                };
            }
            else
            {
                desc = camera.targetTexture.descriptor;
                desc.msaaSamples = msaaSamples;
                desc.width = cameraData.ScaledWidth;
                desc.height = cameraData.ScaledHeight;
                
                if (camera.cameraType == CameraType.SceneView && !isHdrEnabled)
                {
                    desc.graphicsFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
                }
                
                // SystemInfo.SupportsRenderTextureFormat(camera.targetTexture.descriptor.colorFormat)
                // will assert on R8_SINT since it isn't a valid value of RenderTextureFormat.
                // If this is fixed then we can implement debug statement to the user explaining why some
                // RenderTextureFormats available resolves in a black render texture when no warning or error
                // is given.
            }
            
            desc.enableRandomWrite = false;
            desc.bindMS = false;
            desc.useDynamicScale = camera.allowDynamicResolution;
            
            // check that the requested MSAA samples count is supported by the current platform. If it's not supported,
            // replace the requested desc.msaaSamples value with the actual value the engine falls back to
            desc.msaaSamples = SystemInfo.GetRenderTextureSupportedMSAASampleCount(desc);
            
            // if the target platform doesn't support storing multisampled RTs and we are doing any offscreen passes, using a Load load action on the subsequent passes
            // will result in loading Resolved data, which on some platforms is discarded, resulting in losing the results of the previous passes.
            // As a workaround we disable MSAA to make sure that the results of previous passes are stored. (fix for Case 1247423).
            if (!SystemInfo.supportsStoreAndResolveAction)
                desc.msaaSamples = 1;
            
            return desc;
        }
        
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        internal static class ShaderPropertyID
        {
            public static readonly int _Time = Shader.PropertyToID(nameof(_Time));
            public static readonly int _SinTime = Shader.PropertyToID(nameof(_SinTime));
            public static readonly int _CosTime = Shader.PropertyToID(nameof(_CosTime));
            public static readonly int unity_DeltaTime = Shader.PropertyToID(nameof(unity_DeltaTime));
            public static readonly int _TimeParameters = Shader.PropertyToID(nameof(_TimeParameters));
            public static readonly int _LastTimeParameters = Shader.PropertyToID(nameof(_LastTimeParameters));
            
            public static readonly int scaledScreenParams = Shader.PropertyToID("_ScaledScreenParams");
            public static readonly int worldSpaceCameraPos = Shader.PropertyToID("_WorldSpaceCameraPos");
            public static readonly int screenParams = Shader.PropertyToID("_ScreenParams");
            public static readonly int projectionParams = Shader.PropertyToID("_ProjectionParams");
            public static readonly int zBufferParams = Shader.PropertyToID("_ZBufferParams");
            public static readonly int orthoParams = Shader.PropertyToID("unity_OrthoParams");
            public static readonly int globalMipBias = Shader.PropertyToID("_GlobalMipBias");
            
            public static readonly int screenSize = Shader.PropertyToID("_ScreenSize");
            public static readonly int screenCoordScaleBias = Shader.PropertyToID("_ScreenCoordScaleBias");
            public static readonly int screenSizeOverride = Shader.PropertyToID("_ScreenSizeOverride");
            
            public static readonly int unity_MatrixInvV = Shader.PropertyToID(nameof(unity_MatrixInvV));
            public static readonly int unity_MatrixInvP = Shader.PropertyToID(nameof(unity_MatrixInvP));
            public static readonly int unity_MatrixInvVP = Shader.PropertyToID(nameof(unity_MatrixInvVP));
            
            public static readonly int unity_WorldToCamera = Shader.PropertyToID(nameof(unity_WorldToCamera));
            public static readonly int unity_CameraToWorld = Shader.PropertyToID(nameof(unity_CameraToWorld));
            
            public static readonly int unity_CameraWorldClipPlanes = Shader.PropertyToID(nameof(unity_CameraWorldClipPlanes));
        }
        
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        internal static class ShaderGlobalKeywords
        {
            public static GlobalKeyword SCREEN_COORD_OVERRIDE;
            
            public static void InitializeShaderGlobalKeywords()
            {
                // Init all keywords upfront
                SCREEN_COORD_OVERRIDE = GlobalKeyword.Create(ShaderKeywordStrings.SCREEN_COORD_OVERRIDE);
            }
        }
        
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        internal static class ShaderKeywordStrings
        {
            public const string SCREEN_COORD_OVERRIDE = nameof(SCREEN_COORD_OVERRIDE);
        }
    }
}