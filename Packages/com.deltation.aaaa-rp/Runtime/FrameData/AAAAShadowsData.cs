using DELTation.AAAARP.Core;
using DELTation.AAAARP.Data;
using DELTation.AAAARP.Lighting;
using DELTation.AAAARP.Passes;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.FrameData
{
    public class AAAAShadowsData : ContextItem
    {
        internal ShadowMapPool ShadowMapPool { get; set; }
        public AAAATextureSize ShadowMapResolution { get; private set; }
        public NativeList<ShadowLight> ShadowLights { get; private set; }
        public NativeHashMap<int, int> VisibleToShadowLightMapping { get; private set; }

        public void Init(in CullingResults cullingResults,
            AAAACameraData cameraData, AAAALightingSettings.ShadowSettings shadowSettings)
        {
            ShadowMapResolution = shadowSettings.Resolution;
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
                                ShadowMapAllocation = ShadowMapPool.Allocate(ShadowMapResolution),
                            }
                        );
                        VisibleToShadowLightMapping.Add(visibleLightIndex, shadowLights.Length - 1);
                    }
                }
            }

            if (shadowLights.Length > 0)
            {
                Camera camera = cameraData.Camera;
                float cameraNearPlane = camera.nearClipPlane;
                float cameraFarPlane = camera.farClipPlane;
                var cameraFrustumCorners = new NativeArray<float3>(8, Allocator.Temp)
                {
                    [0] = camera.ViewportToWorldPoint(new Vector3(0, 0, cameraNearPlane)),
                    [1] = camera.ViewportToWorldPoint(new Vector3(0, 0, cameraFarPlane)),
                    [2] = camera.ViewportToWorldPoint(new Vector3(0, 1, cameraNearPlane)),
                    [3] = camera.ViewportToWorldPoint(new Vector3(0, 1, cameraFarPlane)),
                    [4] = camera.ViewportToWorldPoint(new Vector3(1, 0, cameraNearPlane)),
                    [5] = camera.ViewportToWorldPoint(new Vector3(1, 0, cameraFarPlane)),
                    [6] = camera.ViewportToWorldPoint(new Vector3(1, 1, cameraNearPlane)),
                    [7] = camera.ViewportToWorldPoint(new Vector3(1, 1, cameraFarPlane)),
                };

                for (int index = 0; index < shadowLights.Length; index++)
                {
                    ref ShadowLight shadowLight = ref shadowLights.ElementAtRef(index);
                    ref readonly VisibleLight visibleLight = ref visibleLights.ElementAtRefReadonly(shadowLight.VisibleLightIndex);

                    float splitNear = 0.0f;
                    float splitFar = math.min(cameraFarPlane, shadowSettings.MaxDistance);
                    Quaternion lightRotation = visibleLight.localToWorldMatrix.rotation;
                    int shadowMapResolution = (int) ShadowMapResolution;
                    AAAAShadowUtils.ComputeDirectionalLightShadowMatrices(cameraFrustumCorners, shadowMapResolution, cameraFarPlane, splitNear, splitFar,
                        lightRotation, out float4x4 lightView, out float4x4 lightProjection
                    );

                    Vector3 cameraPosition = visibleLight.localToWorldMatrix.GetPosition();

                    Matrix4x4 lightViewProjection = math.mul(lightProjection, lightView);

                    const bool renderIntoTexture = true;
                    shadowLight.CullingView = new GPUCullingPass.CullingViewParameters
                    {
                        ViewProjectionMatrix = lightViewProjection,
                        GPUViewProjectionMatrix = GL.GetGPUProjectionMatrix(lightViewProjection, renderIntoTexture),
                        CameraPosition = cameraPosition,
                        CameraRight = lightRotation * Vector3.right,
                        CameraUp = lightRotation * Vector3.up,
                        PixelSize = new Vector2(shadowMapResolution, shadowMapResolution),
                        IsPerspective = false,
                    };
                    shadowLight.ViewMatrix = lightView;
                    shadowLight.GPUProjectionMatrix = GL.GetGPUProjectionMatrix(lightProjection, renderIntoTexture);
                    shadowLight.SlopeBias = AAAAShadowUtils.GetBaseShadowBias(false, 0.0f) * shadowSettings.SlopeBias;
                }
            }
        }

        public override void Reset()
        {
            ShadowMapResolution = 0;
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
            internal ShadowMapPool.Allocation ShadowMapAllocation;
        }
    }
}