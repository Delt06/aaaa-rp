using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;
using float3 = Unity.Mathematics.float3;
using float3x3 = Unity.Mathematics.float3x3;
using float4x4 = Unity.Mathematics.float4x4;

namespace DELTation.AAAARP.Lighting
{
    public static class AAAAShadowUtils
    {
        public static void ComputeDirectionalLightShadowMatrices(NativeArray<float3> cameraFrustumCorners, int resolution, float farPlane, float splitNear,
            float splitFar, quaternion lightRotation, out float4x4 lightView, out float4x4 lightProjection)
        {
            // From Wicked Engine: https://github.com/turanszkij/WickedEngine/blob/84adc794752a4b12d2551fef383d4872726a9255/WickedEngine/wiRenderer.cpp#L2735
            quaternion invLightRotation = inverse(lightRotation);
            float3 lightForward = normalize(rotate(invLightRotation, float3(0, 0, 1)));
            float3 lightUp = normalize(rotate(invLightRotation, float3(0, 1, 0)));
            lightView = LookRotation(lightForward, lightUp);

            float splitNearNormalized = splitNear / farPlane;
            float splitFarNormalized = splitFar / farPlane;
            var corners = new NativeArray<float3>(8, Allocator.Temp)
            {
                [0] = transform(lightView, lerp(cameraFrustumCorners[0], cameraFrustumCorners[1], splitNearNormalized)),
                [1] = transform(lightView, lerp(cameraFrustumCorners[0], cameraFrustumCorners[1], splitFarNormalized)),
                [2] = transform(lightView, lerp(cameraFrustumCorners[2], cameraFrustumCorners[3], splitNearNormalized)),
                [3] = transform(lightView, lerp(cameraFrustumCorners[2], cameraFrustumCorners[3], splitFarNormalized)),
                [4] = transform(lightView, lerp(cameraFrustumCorners[4], cameraFrustumCorners[5], splitNearNormalized)),
                [5] = transform(lightView, lerp(cameraFrustumCorners[4], cameraFrustumCorners[5], splitFarNormalized)),
                [6] = transform(lightView, lerp(cameraFrustumCorners[6], cameraFrustumCorners[7], splitNearNormalized)),
                [7] = transform(lightView, lerp(cameraFrustumCorners[6], cameraFrustumCorners[7], splitFarNormalized)),
            };

            // Compute cascade bounding sphere center:
            float3 center = float3.zero;
            foreach (float3 corner in corners)
            {
                center += corner;
            }
            center /= corners.Length;

            // Compute cascade bounding sphere radius:
            float radius = 0;
            foreach (float3 corner in corners)
            {
                radius = max(radius, length(corner - center));
            }

            // Fit AABB onto bounding sphere:
            float3 aabbMin = center - radius;
            float3 aabbMax = center + radius;

            // Snap cascade to texel grid:
            float3 extent = aabbMax - aabbMin;
            float3 texelSize = extent / resolution;
            aabbMin = floor(aabbMin / texelSize) * texelSize;
            aabbMax = floor(aabbMax / texelSize) * texelSize;
            center = (aabbMin + aabbMax) * 0.5f;

            // Extrude bounds to avoid early shadow clipping:
            float extrusion = abs(center.z - aabbMin.z);
            extrusion = max(extrusion, min(1500.0f, farPlane) * 0.5f);
            aabbMin.z = center.z - extrusion;
            aabbMax.z = center.z + extrusion;

            // notice reversed Z!
            lightProjection = float4x4.OrthoOffCenter(aabbMin.x, aabbMax.x, aabbMin.y, aabbMax.y, aabbMax.z, aabbMin.z);
        }

        private static float4x4 LookRotation(float3 forward, float3 up)
        {
            var rot = float3x3.LookRotation(forward, up);

            float4x4 matrix;
            matrix.c0 = float4(rot.c0, 0.0f);
            matrix.c1 = float4(rot.c1, 0.0f);
            matrix.c2 = float4(rot.c2, 0.0f);
            matrix.c3 = float4(0.0f, 0.0f, 0.0f, 1.0f);
            return matrix;
        }

        public static Matrix4x4 GetWorldToShadowCoordsMatrix(Matrix4x4 projectionMatrix)
        {
            if (SystemInfo.usesReversedZBuffer)
            {
                projectionMatrix.m20 = -projectionMatrix.m20;
                projectionMatrix.m21 = -projectionMatrix.m21;
                projectionMatrix.m22 = -projectionMatrix.m22;
                projectionMatrix.m23 = -projectionMatrix.m23;
            }

            // Maps texture space coordinates from [-1,1] to [0,1]
            Matrix4x4 textureScaleAndBias = Matrix4x4.identity;
            textureScaleAndBias.m00 = 0.5f;
            textureScaleAndBias.m11 = 0.5f;
            textureScaleAndBias.m22 = 0.5f;
            textureScaleAndBias.m03 = 0.5f;
            textureScaleAndBias.m23 = 0.5f;
            textureScaleAndBias.m13 = 0.5f;

            // Apply texture scale and offset to save a MAD in shader.
            return textureScaleAndBias * projectionMatrix;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        // https://github.com/Unity-Technologies/Graphics/blob/e42df452b62857a60944aed34f02efa1bda50018/Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/Light/HDGpuLightsBuilder.LightLoop.cs#L925
        public static float GetBaseShadowBias(bool isHighQuality, float softness)
        {
            // Bias
            // This base bias is a good value if we expose a [0..1] since values within [0..5] are empirically shown to be sensible for the slope-scale bias with the width of our PCF.
            float baseBias = 5.0f;

            // If we are PCSS, the blur radius can be quite big, hence we need to tweak up the slope bias
            if (isHighQuality && softness > 0.01f)
            {
                // maxBaseBias is an empirically set value, also the lerp stops at a shadow softness of 0.05, then is clamped.
                float maxBaseBias = 18.0f;
                baseBias = Mathf.Lerp(baseBias, maxBaseBias, Mathf.Min(1.0f, softness * 100 / 5));
            }

            return baseBias;
        }
    }
}