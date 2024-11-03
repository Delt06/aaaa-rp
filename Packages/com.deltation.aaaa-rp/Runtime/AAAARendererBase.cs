using System;
using System.Collections.Generic;
using DELTation.AAAARP.Debugging;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Passes;
using DELTation.AAAARP.Utils;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using static DELTation.AAAARP.AAAARenderPipelineCore;

namespace DELTation.AAAARP
{
    public abstract partial class AAAARendererBase : IDisposable
    {
        private readonly List<AAAARenderPassBase> _activeRenderPassQueue = new(32);

        private RTHandle _currentColorBuffer;
        private RTHandle _currentDepthBuffer;

        protected AAAARendererBase(AAAARawBufferClear rawBufferClear)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            DebugHandler = new AAAADebugHandler(rawBufferClear);
#endif
        }

        public RTHandle CurrentColorBuffer => _currentColorBuffer;
        public RTHandle CurrentDepthBuffer => _currentDepthBuffer;

        internal ContextContainer FrameData { get; } = new();

        internal AAAADebugHandler DebugHandler { get; }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (_currentColorBuffer != null)
            {
                RTHandles.Release(_currentColorBuffer);
                _currentColorBuffer = null;
            }

            if (_currentDepthBuffer != null)
            {
                RTHandles.Release(_currentDepthBuffer);
                _currentDepthBuffer = null;
            }

            foreach (AAAARenderPassBase renderPassBase in _activeRenderPassQueue)
            {
                if (renderPassBase is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _activeRenderPassQueue.Clear();
            DebugHandler?.Dispose();

            Dispose(true);
        }

        ~AAAARendererBase()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing) { }

        internal void EnqueuePass([NotNull] AAAARenderPassBase pass)
        {
            _activeRenderPassQueue.Add(pass);
        }

        public void RecordRenderGraph(RenderGraph renderGraph, ScriptableRenderContext context)
        {
            using (new ProfilingScope(Profiling.RecordRenderGraph))
            {
                using (new ProfilingScope(Profiling.SortRenderPasses))
                {
                    // Sort the render pass queue
                    StablePassSort(_activeRenderPassQueue);
                }

                InitRenderGraphFrame(renderGraph);

                AAAAResourceData resourceData = FrameData.Get<AAAAResourceData>();
                SetupRenderGraphCameraProperties(renderGraph, resourceData.IsActiveTargetBackBuffer);

                using (new ProfilingScope(Profiling.RecordRenderGraphImpl))
                {
                    RecordRenderGraphImpl(renderGraph, context);
                }
            }
        }

        private static void StablePassSort(List<AAAARenderPassBase> passes)
        {
            for (int i = 1; i < passes.Count; ++i)
            {
                AAAARenderPassBase curr = passes[i];

                int j = i - 1;
                for (; j >= 0 && curr < passes[j]; --j)
                {
                    passes[j + 1] = passes[j];
                }

                passes[j + 1] = curr;
            }
        }

        private void InitRenderGraphFrame(RenderGraph renderGraph)
        {
            using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass("InitFrame", out PassData passData, Profiling.InitRenderGraphFrame);
            passData.Renderer = this;

            builder.AllowPassCulling(false);

            builder.SetRenderFunc((PassData data, UnsafeGraphContext rgContext) =>
                {
                    UnsafeCommandBuffer cmd = rgContext.cmd;
#if UNITY_EDITOR
                    float time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
#else
                    float time = Time.time;
#endif
                    float deltaTime = Time.deltaTime;
                    float smoothDeltaTime = Time.smoothDeltaTime;

                    data.Renderer.ClearRenderingState(cmd);
                    SetShaderTimeValues(cmd, time, deltaTime, smoothDeltaTime);
                }
            );
        }

        private void RecordRenderGraphImpl(RenderGraph renderGraph, ScriptableRenderContext context)
        {
            foreach (AAAARenderPassBase pass in _activeRenderPassQueue)
            {
                pass.RecordRenderGraph(renderGraph, FrameData);
            }
        }

        internal void SetupRenderGraphCameraProperties(RenderGraph renderGraph, bool isTargetBackbuffer)
        {
            using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("SetupCameraProperties", out PassData passData,
                Profiling.SetupRenderGraphCameraProperties
            );

            passData.Renderer = this;
            passData.CameraData = FrameData.Get<AAAACameraData>();
            passData.ScaledCameraTargetSizeCopy = new Vector2Int(passData.CameraData.ScaledWidth, passData.CameraData.ScaledHeight);
            passData.IsTargetBackbuffer = isTargetBackbuffer;

            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true);

            builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    bool yFlip = !SystemInfo.graphicsUVStartsAtTop || data.IsTargetBackbuffer;
                    context.cmd.SetupCameraProperties(data.CameraData.Camera);
                    data.Renderer.SetPerCameraShaderVariables(context.cmd, data.CameraData, data.ScaledCameraTargetSizeCopy, !yFlip);
                    data.Renderer.SetPerCameraClippingPlaneProperties(context.cmd, in data.CameraData, !yFlip);

#if UNITY_EDITOR
                    float time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
#else
                    float time = Time.time;
#endif
                    float deltaTime = Time.deltaTime;
                    float smoothDeltaTime = Time.smoothDeltaTime;

                    // Reset shader time variables as they were overridden in SetupCameraProperties. If we don't do it we might have a mismatch between shadows and main rendering
                    SetShaderTimeValues(context.cmd, time, deltaTime, smoothDeltaTime);
                }
            );
        }

        private void ClearRenderingState(UnsafeCommandBuffer cmd) { }

        private void SetPerCameraShaderVariables(RasterCommandBuffer cmd, AAAACameraData cameraData, Vector2Int cameraTargetSizeCopy, bool isTargetFlipped)
        {
            Camera camera = cameraData.Camera;

            float scaledCameraTargetWidth = cameraTargetSizeCopy.x;
            float scaledCameraTargetHeight = cameraTargetSizeCopy.y;
            float cameraWidth = camera.pixelWidth;
            float cameraHeight = camera.pixelHeight;

            if (camera.allowDynamicResolution)
            {
                scaledCameraTargetWidth *= ScalableBufferManager.widthScaleFactor;
                scaledCameraTargetHeight *= ScalableBufferManager.heightScaleFactor;
            }

            float near = camera.nearClipPlane;
            float far = camera.farClipPlane;
            float invNear = Mathf.Approximately(near, 0.0f) ? 0.0f : 1.0f / near;
            float invFar = Mathf.Approximately(far, 0.0f) ? 0.0f : 1.0f / far;
            float isOrthographic = camera.orthographic ? 1.0f : 0.0f;

            // From http://www.humus.name/temp/Linearize%20depth.txt
            // But as depth component textures on OpenGL always return in 0..1 range (as in D3D), we have to use
            // the same constants for both D3D and OpenGL here.
            // OpenGL would be this:
            // zc0 = (1.0 - far / near) / 2.0;
            // zc1 = (1.0 + far / near) / 2.0;
            // D3D is this:
            float zc0 = 1.0f - far * invNear;
            float zc1 = far * invNear;

            var zBufferParams = new Vector4(zc0, zc1, zc0 * invFar, zc1 * invFar);

            if (SystemInfo.usesReversedZBuffer)
            {
                zBufferParams.y += zBufferParams.x;
                zBufferParams.x = -zBufferParams.x;
                zBufferParams.w += zBufferParams.z;
                zBufferParams.z = -zBufferParams.z;
            }

            // Projection flip sign logic is very deep in GfxDevice::SetInvertProjectionMatrix
            // This setup is tailored especially for overlay camera game view
            // For other scenarios this will be overwritten correctly by SetupCameraProperties
            float projectionFlipSign = isTargetFlipped ? -1.0f : 1.0f;
            var projectionParams = new Vector4(projectionFlipSign, near, far, 1.0f * invFar);
            cmd.SetGlobalVector(ShaderPropertyID.projectionParams, projectionParams);

            var orthoParams = new Vector4(camera.orthographicSize * cameraData.AspectRatio, camera.orthographicSize, 0.0f, isOrthographic);

            // Camera and Screen variables as described in https://docs.unity3d.com/Manual/SL-UnityShaderVariables.html
            cmd.SetGlobalVector(ShaderPropertyID.worldSpaceCameraPos, cameraData.WorldSpaceCameraPos);
            cmd.SetGlobalVector(ShaderPropertyID.screenParams, new Vector4(cameraWidth, cameraHeight, 1.0f + 1.0f / cameraWidth, 1.0f + 1.0f / cameraHeight));
            cmd.SetGlobalVector(ShaderPropertyID.scaledScreenParams,
                new Vector4(scaledCameraTargetWidth, scaledCameraTargetHeight, 1.0f + 1.0f / scaledCameraTargetWidth, 1.0f + 1.0f / scaledCameraTargetHeight)
            );
            cmd.SetGlobalVector(ShaderPropertyID.zBufferParams, zBufferParams);
            cmd.SetGlobalVector(ShaderPropertyID.orthoParams, orthoParams);

            cmd.SetGlobalVector(ShaderPropertyID.screenSize,
                new Vector4(scaledCameraTargetWidth, scaledCameraTargetHeight, 1.0f / scaledCameraTargetWidth, 1.0f / scaledCameraTargetHeight)
            );
            cmd.SetKeyword(ShaderGlobalKeywords.SCREEN_COORD_OVERRIDE, cameraData.UseScreenCoordOverride);
            cmd.SetGlobalVector(ShaderPropertyID.screenSizeOverride, cameraData.ScreenSizeOverride);
            cmd.SetGlobalVector(ShaderPropertyID.screenCoordScaleBias, cameraData.ScreenCoordScaleBias);

            // Calculate a bias value which corrects the mip lod selection logic when image scaling is active.
            // We clamp this value to 0.0 or less to make sure we don't end up reducing image detail in the downsampling case.
            float mipBias = Mathf.Min(-Mathf.Log(cameraWidth / scaledCameraTargetWidth, 2.0f), 0.0f);
            cmd.SetGlobalVector(ShaderPropertyID.globalMipBias, new Vector2(mipBias, Mathf.Pow(2.0f, mipBias)));

            // Set per camera matrices.
            SetCameraMatrices(cmd, cameraData, true, isTargetFlipped);
        }

        private void SetPerCameraClippingPlaneProperties(RasterCommandBuffer cmd, in AAAACameraData cameraData, bool isTargetFlipped)
        {
            Matrix4x4 projectionMatrix = cameraData.GetGPUProjectionMatrixJittered(isTargetFlipped);
            Matrix4x4 viewMatrix = cameraData.ViewMatrix;

            Matrix4x4 viewProj = CoreMatrixUtils.MultiplyProjectionMatrix(projectionMatrix, viewMatrix, cameraData.Camera.orthographic);
            Plane[] planes = TempCollections.Planes;
            GeometryUtility.CalculateFrustumPlanes(viewProj, planes);

            Vector4[] cameraWorldClipPlanes = TempCollections.VectorPlanes;
            for (int i = 0; i < planes.Length; ++i)
            {
                cameraWorldClipPlanes[i] = new Vector4(planes[i].normal.x, planes[i].normal.y, planes[i].normal.z, planes[i].distance);
            }

            cmd.SetGlobalVectorArray(ShaderPropertyID.unity_CameraWorldClipPlanes, cameraWorldClipPlanes);
        }

        internal static void SetCameraMatrices(RasterCommandBuffer cmd, AAAACameraData cameraData, bool setInverseMatrices, bool isTargetFlipped)
        {
            Matrix4x4 viewMatrix = cameraData.ViewMatrix;
            Matrix4x4 projectionMatrix = cameraData.GetProjectionMatrixJittered();

            // Set the default view/projection, note: projectionMatrix will be set as a gpu-projection (gfx api adjusted) for rendering.
            cmd.SetViewProjectionMatrices(viewMatrix, projectionMatrix);

            if (setInverseMatrices)
            {
                Matrix4x4
                    gpuProjectionMatrix =
                        cameraData.GetGPUProjectionMatrixJittered(isTargetFlipped
                        );
                var inverseViewMatrix = Matrix4x4.Inverse(viewMatrix);
                var inverseProjectionMatrix = Matrix4x4.Inverse(gpuProjectionMatrix);
                Matrix4x4 inverseViewProjection = inverseViewMatrix * inverseProjectionMatrix;

                // There's an inconsistency in handedness between unity_matrixV and unity_WorldToCamera
                // Unity changes the handedness of unity_WorldToCamera (see Camera::CalculateMatrixShaderProps)
                // we will also change it here to avoid breaking existing shaders. (case 1257518)
                Matrix4x4 worldToCameraMatrix = Matrix4x4.Scale(new Vector3(1.0f, 1.0f, -1.0f)) * viewMatrix;
                Matrix4x4 cameraToWorldMatrix = worldToCameraMatrix.inverse;
                cmd.SetGlobalMatrix(ShaderPropertyID.unity_WorldToCamera, worldToCameraMatrix);
                cmd.SetGlobalMatrix(ShaderPropertyID.unity_CameraToWorld, cameraToWorldMatrix);

                cmd.SetGlobalMatrix(ShaderPropertyID.unity_MatrixInvV, inverseViewMatrix);
                cmd.SetGlobalMatrix(ShaderPropertyID.unity_MatrixInvP, inverseProjectionMatrix);
                cmd.SetGlobalMatrix(ShaderPropertyID.unity_MatrixInvVP, inverseViewProjection);
            }
        }

        private static void SetShaderTimeValues(IBaseCommandBuffer cmd, float time, float deltaTime, float smoothDeltaTime)
        {
            float timeEights = time / 8f;
            float timeFourth = time / 4f;
            float timeHalf = time / 2f;

            float lastTime = time - ShaderUtils.PersistentDeltaTime;

            // Time values
            Vector4 timeVector = time * new Vector4(1f / 20f, 1f, 2f, 3f);
            var sinTimeVector = new Vector4(Mathf.Sin(timeEights), Mathf.Sin(timeFourth), Mathf.Sin(timeHalf), Mathf.Sin(time));
            var cosTimeVector = new Vector4(Mathf.Cos(timeEights), Mathf.Cos(timeFourth), Mathf.Cos(timeHalf), Mathf.Cos(time));
            var deltaTimeVector = new Vector4(deltaTime, 1f / deltaTime, smoothDeltaTime, 1f / smoothDeltaTime);
            var timeParametersVector = new Vector4(time, Mathf.Sin(time), Mathf.Cos(time), 0.0f);
            var lastTimeParametersVector = new Vector4(lastTime, Mathf.Sin(lastTime), Mathf.Cos(lastTime), 0.0f);

            cmd.SetGlobalVector(ShaderPropertyID._Time, timeVector);
            cmd.SetGlobalVector(ShaderPropertyID._SinTime, sinTimeVector);
            cmd.SetGlobalVector(ShaderPropertyID._CosTime, cosTimeVector);
            cmd.SetGlobalVector(ShaderPropertyID.unity_DeltaTime, deltaTimeVector);
            cmd.SetGlobalVector(ShaderPropertyID._TimeParameters, timeParametersVector);
            cmd.SetGlobalVector(ShaderPropertyID._LastTimeParameters, lastTimeParametersVector);
        }

        public void FinishRenderGraphRendering(CommandBuffer cmd) { }

        public void PostRender(ScriptableRenderContext context, Camera camera)
        {
            RenderGizmos(context, camera);
        }

        partial void RenderGizmos(ScriptableRenderContext context, Camera camera);

        public void Clear(Camera camera) { }

        public void ImportBackbuffer(AAAACameraData cameraData)
        {
            RenderTargetIdentifier targetColorId = BuiltinRenderTextureType.CameraTarget;
            RenderTargetIdentifier targetDepthId = BuiltinRenderTextureType.Depth;

            if (cameraData.TargetTexture != null)
            {
                targetColorId = new RenderTargetIdentifier(cameraData.TargetTexture);
                targetDepthId = new RenderTargetIdentifier(cameraData.TargetTexture);
            }

            if (_currentColorBuffer == null)
            {
                _currentColorBuffer = RTHandles.Alloc(targetColorId, "CameraTargetColor");
            }
            else
            {
                RTHandleStaticHelpers.SetRTHandleUserManagedWrapper(ref _currentColorBuffer, targetColorId);
            }

            if (_currentDepthBuffer == null)
            {
                _currentDepthBuffer = RTHandles.Alloc(targetDepthId, "CameraTargetDepth");
            }
            else
            {
                RTHandleStaticHelpers.SetRTHandleUserManagedWrapper(ref _currentDepthBuffer, targetDepthId);
            }
        }

        public void BeginFrame(RenderGraph renderGraph, ScriptableRenderContext context, ContextContainer frameData)
        {
            _activeRenderPassQueue.Clear();

            Setup(renderGraph, context, frameData);
        }

        protected abstract void Setup(RenderGraph renderGraph, ScriptableRenderContext context, ContextContainer frameData);

        public void EndFrame()
        {
            _activeRenderPassQueue.Clear();
        }

        private class PassData
        {
            public AAAACameraData CameraData;
            public bool IsTargetBackbuffer;
            public AAAARendererBase Renderer;

            // The size of the camera target changes during the frame, so we must make a copy of it here to preserve its record-time value.
            public Vector2Int ScaledCameraTargetSizeCopy;
        }

        private static partial class Profiling
        {
            private const string Name = "AAAA";

            public static readonly ProfilingSampler SetupRenderGraphCameraProperties =
                new($"{Name}.{nameof(AAAARendererBase.SetupRenderGraphCameraProperties)}");
            public static readonly ProfilingSampler SortRenderPasses = new($"{Name}.{nameof(SortRenderPasses)}");
            public static readonly ProfilingSampler RecordRenderGraph = new($"{Name}.{nameof(AAAARendererBase.RecordRenderGraph)}");
            public static readonly ProfilingSampler RecordRenderGraphImpl = new($"{Name}.{nameof(AAAARendererBase.RecordRenderGraph)}");
            public static readonly ProfilingSampler InitRenderGraphFrame = new($"{Name}.{nameof(AAAARendererBase.InitRenderGraphFrame)}");
        }
    }
}