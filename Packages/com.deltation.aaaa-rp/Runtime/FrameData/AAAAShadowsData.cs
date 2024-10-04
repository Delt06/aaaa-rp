using DELTation.AAAARP.Core;
using DELTation.AAAARP.Data;
using DELTation.AAAARP.Lighting;
using DELTation.AAAARP.Passes;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.FrameData
{
    public class AAAAShadowsData : ContextItem
    {
        public TextureHandle DirectionalLightShadowMapArray { get; private set; }
        public int ShadowMapResolution { get; private set; }
        public NativeList<ShadowLight> ShadowLights { get; private set; }
        public NativeHashMap<int, int> VisibleToShadowLightMapping { get; private set; }

        public void Init(RenderGraph renderGraph, in CullingResults cullingResults,
            AAAACameraData cameraData, AAAALightingSettings.ShadowSettings shadowSettings)
        {
            ShadowMapResolution = (int) shadowSettings.Resolution;
            {
                TextureDesc textureDesc = AAAARenderingUtils.CreateTextureDesc(nameof(DirectionalLightShadowMapArray),
                    new RenderTextureDescriptor(ShadowMapResolution, ShadowMapResolution, GraphicsFormat.None, GraphicsFormat.D24_UNorm)
                );
                textureDesc.clearBuffer = false;
                textureDesc.dimension = TextureDimension.Tex2DArray;
                textureDesc.slices = AAAALightingConstantBuffer.MaxDirectionalLights;
                DirectionalLightShadowMapArray = renderGraph.CreateTexture(textureDesc);
            }

            ShadowLights = new NativeList<ShadowLight>(cullingResults.visibleLights.Length, Allocator.Temp);
            VisibleToShadowLightMapping = new NativeHashMap<int, int>(ShadowLights.Capacity, Allocator.Temp);
            CollectShadowLights(cullingResults, cameraData, shadowSettings, ShadowLights);
        }

        private void CollectShadowLights(in CullingResults cullingResults, AAAACameraData cameraData,
            AAAALightingSettings.ShadowSettings shadowSettings,
            NativeList<ShadowLight> shadowLights)
        {
            NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;

            for (int visibleLightIndex = 0; visibleLightIndex < visibleLights.Length; visibleLightIndex++)
            {
                ref readonly VisibleLight visibleLight = ref visibleLights.ElementAtRefReadonly(visibleLightIndex);

                if (visibleLight.lightType == LightType.Directional)
                {
                    Light light = visibleLight.light;
                    if (light.shadows != LightShadows.None)
                    {
                        shadowLights.Add(new ShadowLight
                            {
                                LightType = visibleLight.lightType,
                                VisibleLightIndex = visibleLightIndex,
                                NearPlaneOffset = light.shadowNearPlane,
                            }
                        );
                        VisibleToShadowLightMapping.Add(visibleLightIndex, shadowLights.Length - 1);
                    }
                }
            }

            if (shadowLights.Length > 0)
            {
                Camera camera = cameraData.Camera;
                float shadowDistance = math.min(camera.farClipPlane, shadowSettings.MaxDistance);
                var cameraFrustumRays = new NativeArray<Ray>(4, Allocator.Temp)
                {
                    [0] = camera.ViewportPointToRay(new Vector3(0, 0)),
                    [1] = camera.ViewportPointToRay(new Vector3(0, 1)),
                    [2] = camera.ViewportPointToRay(new Vector3(1, 0)),
                    [3] = camera.ViewportPointToRay(new Vector3(1, 1)),
                };

                for (int index = 0; index < shadowLights.Length; index++)
                {
                    ref ShadowLight shadowLight = ref shadowLights.ElementAtRef(index);
                    ref readonly VisibleLight visibleLight = ref visibleLights.ElementAtRefReadonly(shadowLight.VisibleLightIndex);

                    float3 directionToLight = AAAALightingUtils.ExtractDirection(visibleLight.localToWorldMatrix);
                    float3 lightViewCenter = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0));
                    float3 lightViewForward = -directionToLight;
                    var lightViewRotation = Quaternion.LookRotation(lightViewForward);

                    // Invert Z in view space
                    Matrix4x4 lightMatrix = Matrix4x4.Scale(new Vector3(1, 1, -1)) * Matrix4x4.Rotate(Quaternion.Inverse(lightViewRotation)) *
                                            Matrix4x4.Translate(-lightViewCenter);

                    var cameraFrustumPointsLS = new NativeArray<float3>(cameraFrustumRays.Length * 2, Allocator.Temp);
                    for (int i = 0; i < cameraFrustumRays.Length; i++)
                    {
                        Ray ray = cameraFrustumRays[i];

                        cameraFrustumPointsLS[i * 2 + 0] = lightMatrix.MultiplyPoint(ray.origin);
                        cameraFrustumPointsLS[i * 2 + 1] = lightMatrix.MultiplyPoint(ray.GetPoint(shadowDistance));
                    }

                    float3 cameraFrustumBoundsMinLS = float.PositiveInfinity;
                    float3 cameraFrustumBoundsMaxLS = float.NegativeInfinity;

                    foreach (float3 cameraFrustumPointLS in cameraFrustumPointsLS)
                    {
                        cameraFrustumBoundsMinLS = math.min(cameraFrustumBoundsMinLS, cameraFrustumPointLS);
                        cameraFrustumBoundsMaxLS = math.max(cameraFrustumBoundsMaxLS, cameraFrustumPointLS);
                    }

                    float shadowNearPlane = visibleLight.light.shadowNearPlane;
                    var projectionMatrix = Matrix4x4.Ortho(cameraFrustumBoundsMinLS.x, cameraFrustumBoundsMaxLS.x, cameraFrustumBoundsMinLS.y,
                        cameraFrustumBoundsMaxLS.y, shadowNearPlane, shadowDistance
                    );

                    float3 cameraFrustumBoundsCenterLS = (cameraFrustumBoundsMinLS + cameraFrustumBoundsMaxLS) * 0.5f;
                    Vector3 cameraPosition = lightMatrix.inverse.MultiplyPoint(math.float3(cameraFrustumBoundsCenterLS.xy, 0));

                    Matrix4x4 viewProjectionMatrix = projectionMatrix * lightMatrix;

                    const bool renderIntoTexture = true;
                    shadowLight.CullingView = new GPUCullingPass.CullingViewParameters
                    {
                        ViewProjectionMatrix = viewProjectionMatrix,
                        GPUViewProjectionMatrix = GL.GetGPUProjectionMatrix(viewProjectionMatrix, renderIntoTexture),
                        CameraPosition = cameraPosition,
                        CameraRight = lightViewRotation * Vector3.right,
                        CameraUp = lightViewRotation * Vector3.up,
                        PixelSize = new Vector2(ShadowMapResolution, ShadowMapResolution),
                    };
                    shadowLight.ViewMatrix = lightMatrix;
                    shadowLight.GPUProjectionMatrix = GL.GetGPUProjectionMatrix(projectionMatrix, renderIntoTexture);
                    shadowLight.SlopeBias = AAAALightingUtils.GetBaseShadowBias(false, 0.0f) * shadowSettings.SlopeBias;
                }
            }
        }

        public override void Reset()
        {
            ShadowMapResolution = 0;
            DirectionalLightShadowMapArray = TextureHandle.nullHandle;
            ShadowLights = default;
        }

        public struct ShadowLight
        {
            public int VisibleLightIndex;
            public float NearPlaneOffset;
            public LightType LightType;
            public Matrix4x4 ViewMatrix;
            public Matrix4x4 GPUProjectionMatrix;
            public GPUCullingPass.CullingViewParameters CullingView;
            public float SlopeBias;
        }
    }
}