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
using AAAAShadowSettingsComponent = DELTation.AAAARP.Volumes.AAAAShadowSettingsComponent;

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
            AAAAShadowSettingsComponent volumeComponent = cameraData.VolumeStack.GetComponent<AAAAShadowSettingsComponent>();

            ShadowMapResolution = ResolveValue(volumeComponent.Resolution, shadowSettings.Resolution);
            ShadowLights = new NativeList<ShadowLight>(cullingResults.visibleLights.Length, Allocator.Temp);
            VisibleToShadowLightMapping = new NativeHashMap<int, int>(ShadowLights.Capacity, Allocator.Temp);

            var shadowSettingsData = new ShadowSettingsData
            {
                MaxDistance =
                    ResolveValue(volumeComponent.MaxDistance, shadowSettings.MaxDistance),
                DirectionalLightCascades =
                    ResolveValue(volumeComponent.DirectionalLightCascades, shadowSettings.DirectionalLightCascades),
                DirectionalLightCascadeDistance1 =
                    ResolveValue(volumeComponent.DirectionalLightCascadeDistance1, shadowSettings.DirectionalLightCascadeDistance1),
                DirectionalLightCascadeDistance2 =
                    ResolveValue(volumeComponent.DirectionalLightCascadeDistance2, shadowSettings.DirectionalLightCascadeDistance2),
                DirectionalLightCascadeDistance3 =
                    ResolveValue(volumeComponent.DirectionalLightCascadeDistance3, shadowSettings.DirectionalLightCascadeDistance3),
                ShadowFade =
                    ResolveValue(volumeComponent.ShadowFade, shadowSettings.ShadowFade),
                DepthBias =
                    ResolveValue(volumeComponent.DepthBias, shadowSettings.DepthBias),
                PunctualDepthBias =
                    ResolveValue(volumeComponent.PunctualDepthBias, shadowSettings.PunctualDepthBias),
                SlopeBias =
                    ResolveValue(volumeComponent.SlopeBias, shadowSettings.SlopeBias),
            };
            CollectShadowLights(cullingResults, cameraData, shadowSettingsData, ShadowLights);

            ShadowLightSlicesBuffer = renderingData.RenderGraph.CreateBuffer(
                new BufferDesc(shadowSettings.MaxShadowLightSlices, UnsafeUtility.SizeOf<AAAAShadowLightSlice>(), GraphicsBuffer.Target.Structured)
                {
                    name = nameof(ShadowLightSlicesBuffer),
                }
            );
        }

        private static T ResolveValue<T>(VolumeParameter<T> parameter, T fallbackValue) => parameter.overrideState ? parameter.value : fallbackValue;

        private void CollectShadowLights(in CullingResults cullingResults, AAAACameraData cameraData,
            in ShadowSettingsData shadowSettings,
            NativeList<ShadowLight> shadowLights)
        {
            NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;

            for (int visibleLightIndex = 0; visibleLightIndex < visibleLights.Length; visibleLightIndex++)
            {
                ref readonly VisibleLight visibleLight = ref visibleLights.ElementAtRefReadonly(visibleLightIndex);
                Light light = visibleLight.light;
                LightShadows lightShadowsType = light.shadows;
                if (lightShadowsType == LightShadows.None)
                {
                    continue;
                }

                int splitCapacity = visibleLight.lightType switch
                {
                    LightType.Directional => AAAALightingSettings.ShadowSettings.MaxCascades,
                    LightType.Spot => 1,
                    LightType.Point => AAAAShadowUtils.TetrahedronFace.Count,
                    var _ => 0,
                };

                if (splitCapacity == 0)
                {
                    continue;
                }

                shadowLights.Add(new ShadowLight
                    {
                        LightType = visibleLight.lightType,
                        IsSoftShadow = lightShadowsType == LightShadows.Soft,
                        ShadowStrength = light.shadowStrength,
                        VisibleLightIndex = visibleLightIndex,
                        NearPlaneOffset = light.shadowNearPlane,
                        Splits = new NativeList<ShadowLightSplit>(splitCapacity, Allocator.Temp),
                    }
                );
                VisibleToShadowLightMapping.Add(visibleLightIndex, shadowLights.Length - 1);
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

                    if (shadowLight.LightType is LightType.Directional or LightType.Spot or LightType.Point)
                    {
                        Quaternion lightRotation = visibleLight.localToWorldMatrix.rotation;
                        int shadowMapResolution = (int) ShadowMapResolution;
                        Vector3 lightPosition = visibleLight.localToWorldMatrix.GetPosition();
                        Vector3 lightForward = lightRotation * Vector3.forward;
                        Vector3 lightRight = lightRotation * Vector3.right;
                        Vector3 lightUp = lightRotation * Vector3.up;

                        const bool renderIntoTexture = true;

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

                                AAAAShadowUtils.ComputeDirectionalLightShadowMatrices(
                                    cameraFrustumCorners, cameraPosition, cameraFarPlane,
                                    shadowMapResolution, lightRotation, splitNear, splitFar, out float4x4 lightView, out float4x4 lightProjection
                                );

                                Matrix4x4 lightViewProjection = math.mul(lightProjection, lightView);
                                shadowLight.Splits.Add(new ShadowLightSplit
                                    {
                                        ShadowMapAllocation = ShadowMapPool.Allocate(shadowMapResolution),
                                        CullingView = new GPUCullingPass.CullingViewParameters
                                        {
                                            ViewMatrix = lightView,
                                            ViewProjectionMatrix = lightViewProjection,
                                            GPUViewProjectionMatrix = GL.GetGPUProjectionMatrix(lightViewProjection, renderIntoTexture),
                                            BoundingSphereWS = math.float4(cameraPosition, splitFar),
                                            CameraPosition = lightPosition,
                                            CameraForward = lightForward,
                                            CameraRight = lightRight,
                                            CameraUp = lightUp,
                                            PixelSize = new Vector2(shadowMapResolution, shadowMapResolution),
                                            IsPerspective = false,
                                            PassMask = AAAAInstancePassMask.Shadows,
                                        },
                                        GPUProjectionMatrix = GL.GetGPUProjectionMatrix(lightProjection, renderIntoTexture),
                                    }
                                );
                            }
                        }
                        else if (shadowLight.LightType == LightType.Spot)
                        {
                            AAAAShadowUtils.ComputeSpotLightShadowMatrices(
                                lightRotation, lightPosition, visibleLight.spotAngle, shadowLight.NearPlaneOffset, visibleLight.range,
                                out float4x4 lightView, out float4x4 lightProjection
                            );

                            Matrix4x4 lightViewProjection = math.mul(lightProjection, lightView);
                            shadowLight.Splits.Add(new ShadowLightSplit
                                {
                                    ShadowMapAllocation = ShadowMapPool.Allocate(shadowMapResolution),
                                    CullingView = new GPUCullingPass.CullingViewParameters
                                    {
                                        ViewMatrix = lightView,
                                        ViewProjectionMatrix = lightViewProjection,
                                        GPUViewProjectionMatrix = GL.GetGPUProjectionMatrix(lightViewProjection, renderIntoTexture),
                                        BoundingSphereWS = default,
                                        CameraPosition = lightPosition,
                                        CameraForward = lightForward,
                                        CameraRight = lightRight,
                                        CameraUp = lightUp,
                                        PixelSize = new Vector2(shadowMapResolution, shadowMapResolution),
                                        IsPerspective = true,
                                        PassMask = AAAAInstancePassMask.Shadows,
                                    },
                                    GPUProjectionMatrix = GL.GetGPUProjectionMatrix(lightProjection, renderIntoTexture),
                                }
                            );
                        }
                        else if (shadowLight.LightType == LightType.Point)
                        {
                            for (int faceIndex = 0; faceIndex < AAAAShadowUtils.TetrahedronFace.Count; faceIndex++)
                            {
                                AAAAShadowUtils.ComputePointLightShadowMatrices(
                                    lightPosition, shadowLight.NearPlaneOffset, visibleLight.range, faceIndex,
                                    out float4x4 lightView, out float4x4 lightProjection, out AAAAShadowUtils.TetrahedronFace tetrahedronFace
                                );

                                Matrix4x4 lightViewProjection = math.mul(lightProjection, lightView);
                                shadowLight.Splits.Add(new ShadowLightSplit
                                    {
                                        ShadowMapAllocation = ShadowMapPool.Allocate(shadowMapResolution),
                                        CullingView = new GPUCullingPass.CullingViewParameters
                                        {
                                            ViewMatrix = lightView,
                                            ViewProjectionMatrix = lightViewProjection,
                                            GPUViewProjectionMatrix = GL.GetGPUProjectionMatrix(lightViewProjection, renderIntoTexture),
                                            BoundingSphereWS = default,
                                            CameraPosition = lightPosition,
                                            CameraForward = tetrahedronFace.Forward,
                                            CameraRight = tetrahedronFace.Right,
                                            CameraUp = tetrahedronFace.Up,
                                            PixelSize = new Vector2(shadowMapResolution, shadowMapResolution),
                                            IsPerspective = true,
                                            PassMask = AAAAInstancePassMask.Shadows,
                                        },
                                        GPUProjectionMatrix = GL.GetGPUProjectionMatrix(lightProjection, renderIntoTexture),
                                    }
                                );
                            }
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

                    shadowLight.DepthBias = -(shadowLight.LightType == LightType.Directional ? shadowSettings.DepthBias : shadowSettings.PunctualDepthBias);
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

        private struct ShadowSettingsData
        {
            public float MaxDistance;
            public int DirectionalLightCascades;
            public float DirectionalLightCascadeDistance1;
            public float DirectionalLightCascadeDistance2;
            public float DirectionalLightCascadeDistance3;
            public float ShadowFade;
            public float DepthBias;
            public float PunctualDepthBias;
            public float SlopeBias;
        }

        public struct ShadowLight
        {
            public int VisibleLightIndex;
            public float NearPlaneOffset;
            public LightType LightType;
            public bool IsSoftShadow;
            public float ShadowStrength;
            public float DepthBias;
            public float SlopeBias;
            public float2 FadeParams;
            public int Resolution;

            public NativeList<ShadowLightSplit> Splits;
        }

        public struct ShadowLightSplit
        {
            public Matrix4x4 GPUProjectionMatrix;
            public GPUCullingPass.CullingViewParameters CullingView;
            internal ShadowMapPool.Allocation ShadowMapAllocation;
        }
    }
}