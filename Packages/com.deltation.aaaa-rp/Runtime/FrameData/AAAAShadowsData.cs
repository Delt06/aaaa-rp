using DELTation.AAAARP.Core;
using DELTation.AAAARP.Data;
using DELTation.AAAARP.Lighting;
using DELTation.AAAARP.Passes;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.FrameData
{
    public class AAAAShadowsData : ContextItem
    {
        public BufferHandle ShadowLightSlicesBuffer;
        internal ShadowMapPool ShadowMapPool { get; set; }
        public AAAATextureSize ShadowMapResolution { get; private set; }
        public NativeList<ShadowLight> ShadowLights { get; private set; }
        public NativeHashMap<int, int> VisibleToShadowLightMapping { get; private set; }

        public void Init(AAAARenderingData renderingData, AAAACameraData cameraData, AAAALightingSettings.ShadowSettings shadowSettings)
        {
            ref readonly CullingResults cullingResults = ref renderingData.CullingResults;
            ShadowMapResolution = shadowSettings.Resolution;
            ShadowLights = new NativeList<ShadowLight>(cullingResults.visibleLights.Length, Allocator.Temp);
            VisibleToShadowLightMapping = new NativeHashMap<int, int>(ShadowLights.Capacity, Allocator.Temp);
            CollectShadowLights(cullingResults, cameraData, shadowSettings, ShadowLights);

            ShadowLightSlicesBuffer = renderingData.RenderGraph.CreateBuffer(
                new BufferDesc(shadowSettings.MaxShadowLightSlices, UnsafeUtility.SizeOf<AAAAShadowLightSlice>(), GraphicsBuffer.Target.Structured)
                {
                    name = nameof(ShadowLightSlicesBuffer),
                }
            );
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
                                Splits = new NativeList<ShadowLightSplit>(4, Allocator.Temp),
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
                float3 cameraPosition = camera.transform.position;
                float shadowDistance = math.min(cameraFarPlane, shadowSettings.MaxDistance);

                for (int index = 0; index < shadowLights.Length; index++)
                {
                    ref ShadowLight shadowLight = ref shadowLights.ElementAtRef(index);
                    ref readonly VisibleLight visibleLight = ref visibleLights.ElementAtRefReadonly(shadowLight.VisibleLightIndex);

                    Quaternion lightRotation = visibleLight.localToWorldMatrix.rotation;
                    int shadowMapResolution = (int) ShadowMapResolution;
                    Vector3 lightPosition = visibleLight.localToWorldMatrix.GetPosition();
                    Vector3 lightRight = lightRotation * Vector3.right;
                    Vector3 lightUp = lightRotation * Vector3.up;

                    if (shadowLight.LightType == LightType.Directional)
                    {
                        var cascadeDistances = new Vector4(
                            shadowSettings.DirectionalLightCascadeDistance1,
                            shadowSettings.DirectionalLightCascadeDistance2,
                            shadowSettings.DirectionalLightCascadeDistance3,
                            1.0f
                        );
                        int cascadeCount = shadowSettings.DirectionalLightCascades;

                        for (int cascadeIndex = 0; cascadeIndex < cascadeCount; cascadeIndex++)
                        {
                            float splitNear = cascadeIndex == 0 ? 0.0f : shadowDistance * cascadeDistances[cascadeIndex - 1];
                            float splitFar = cascadeIndex == cascadeCount - 1 ? shadowDistance : shadowDistance * cascadeDistances[cascadeIndex];
                            int splitResolution = math.max(1, shadowMapResolution);

                            AAAAShadowUtils.ComputeDirectionalLightShadowMatrices(
                                cameraFrustumCorners, splitResolution, cameraFarPlane,
                                splitNear, splitFar,
                                lightRotation, out float4x4 lightView, out float4x4 lightProjection
                            );

                            Matrix4x4 lightViewProjection = math.mul(lightProjection, lightView);
                            const bool renderIntoTexture = true;
                            shadowLight.Splits.Add(new ShadowLightSplit
                                {
                                    ShadowMapAllocation = ShadowMapPool.Allocate(splitResolution),
                                    CullingView = new GPUCullingPass.CullingViewParameters
                                    {
                                        ViewProjectionMatrix = lightViewProjection,
                                        GPUViewProjectionMatrix = GL.GetGPUProjectionMatrix(lightViewProjection, renderIntoTexture),
                                        BoundingSphere = math.float4(cameraPosition, splitFar * splitFar),
                                        CameraPosition = lightPosition,
                                        CameraRight = lightRight,
                                        CameraUp = lightUp,
                                        PixelSize = new Vector2(splitResolution, splitResolution),
                                        IsPerspective = false,
                                    },
                                    ViewMatrix = lightView,
                                    GPUProjectionMatrix = GL.GetGPUProjectionMatrix(lightProjection, renderIntoTexture),
                                }
                            );
                        }

                        AAAAShadowUtils.GetScaleAndBiasForLinearDistanceFade(
                            shadowDistance * shadowDistance, shadowSettings.ShadowFade, out float shadowFadeScale, out float shadowFadeBias
                        );
                        shadowLight.FadeParams = math.float2(shadowFadeScale, shadowFadeBias);
                    }
                    else
                    {
                        shadowLight.FadeParams = math.float2(0.0f, 0.0f);
                    }

                    shadowLight.SlopeBias = AAAAShadowUtils.GetBaseShadowBias(false, 0.0f) * shadowSettings.SlopeBias;
                }
            }
        }

        public override void Reset()
        {
            ShadowMapResolution = 0;
            ShadowLights = default;
            ShadowLightSlicesBuffer = BufferHandle.nullHandle;
        }

        public struct ShadowLight
        {
            public int VisibleLightIndex;
            public float NearPlaneOffset;
            public LightType LightType;
            public float SlopeBias;
            public float2 FadeParams;

            public NativeList<ShadowLightSplit> Splits;
        }

        public struct ShadowLightSplit
        {
            public Matrix4x4 ViewMatrix;
            public Matrix4x4 GPUProjectionMatrix;
            public GPUCullingPass.CullingViewParameters CullingView;
            internal ShadowMapPool.Allocation ShadowMapAllocation;
        }
    }
}