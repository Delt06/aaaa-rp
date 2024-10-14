using DELTation.AAAARP.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.FrameData
{
    public class AAAACameraData : ContextItem
    {
        public AAAAAmbientOcclusionTechnique AmbientOcclusionTechnique;
        public AAAAAntiAliasingTechnique AntiAliasingTechnique;
        public float AspectRatio;
        public Color BackgroundColor;
        public Camera Camera;
        public RenderTextureDescriptor CameraTargetDescriptor;
        public CameraType CameraType;
        public bool ClearColor;
        public bool ClearDepth;
        public float FSRSharpness;
        public HDRColorBufferPrecision HDRColorBufferPrecision;
        public bool IsAlphaOutputEnabled;
        public bool IsDefaultViewport;
        public bool IsHdrEnabled;
        public Matrix4x4 JitterMatrix;
        public int PixelHeight;
        public Rect PixelRect;
        public int PixelWidth;
        public bool PostProcessingEnabled;
        public Matrix4x4 ProjectionMatrix;
        public AAAARendererBase Renderer;
        public float RenderScale;
        public Vector4 ScreenCoordScaleBias;
        public Vector4 ScreenSizeOverride;
        public RenderTexture TargetTexture;
        public AAAAUpscalingTechnique UpscalingTechnique;
        public bool UseScreenCoordOverride;
        public Matrix4x4 ViewMatrix;
        public Vector3 WorldSpaceCameraPos;

        public int ScaledWidth => Mathf.Max(1, (int) (Camera.pixelWidth * RenderScale));
        public int ScaledHeight => Mathf.Max(1, (int) (Camera.pixelHeight * RenderScale));

        public override void Reset()
        {
            Renderer = default;

            Camera = default;
            CameraType = CameraType.Game;
            ClearDepth = false;
            ClearColor = false;
            BackgroundColor = Color.black;
            CameraTargetDescriptor = default;
            IsDefaultViewport = false;
            PixelRect = default;
            UseScreenCoordOverride = false;
            ScreenSizeOverride = default;
            ScreenCoordScaleBias = default;
            PixelWidth = 0;
            PixelHeight = 0;
            AspectRatio = 0.0f;
            RenderScale = 1.0f;
            IsHdrEnabled = false;
            IsAlphaOutputEnabled = default;
            HDRColorBufferPrecision = HDRColorBufferPrecision._32Bits;
            TargetTexture = null;

            ProjectionMatrix = Matrix4x4.identity;
            ViewMatrix = Matrix4x4.identity;
            JitterMatrix = Matrix4x4.identity;
            WorldSpaceCameraPos = default;

            AmbientOcclusionTechnique = default;
            AntiAliasingTechnique = default;
            UpscalingTechnique = default;
            PostProcessingEnabled = false;
        }

        internal void SetViewProjectionAndJitterMatrix(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            ViewMatrix = viewMatrix;
            ProjectionMatrix = projectionMatrix;
            JitterMatrix = Matrix4x4.identity;
        }

        internal Matrix4x4 GetGPUProjectionMatrixJittered(bool renderIntoTexture) =>
            JitterMatrix * GL.GetGPUProjectionMatrix(ProjectionMatrix, renderIntoTexture);

        internal Matrix4x4 GetProjectionMatrixJittered() => JitterMatrix * ProjectionMatrix;

        public bool IsRenderTargetProjectionMatrixFlipped(RTHandle color, RTHandle depth = null)
        {
            if (!SystemInfo.graphicsUVStartsAtTop)
                return true;

            return TargetTexture != null || IsHandleYFlipped(color ?? depth);
        }

        public bool IsHandleYFlipped(RTHandle handle)
        {
            if (!SystemInfo.graphicsUVStartsAtTop)
                return true;

            if (CameraType is CameraType.SceneView or CameraType.Preview)
                return true;

            var handleID = new RenderTargetIdentifier(handle.nameID, 0, CubemapFace.Unknown, 0);
            bool isBackbuffer = handleID == BuiltinRenderTextureType.CameraTarget || handleID == BuiltinRenderTextureType.Depth;
            return !isBackbuffer;
        }
    }
}