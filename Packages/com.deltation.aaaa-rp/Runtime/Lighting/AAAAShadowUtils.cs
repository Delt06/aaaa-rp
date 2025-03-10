using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;
using static Unity.Mathematics.math;
using float3 = Unity.Mathematics.float3;
using float3x3 = Unity.Mathematics.float3x3;
using float4x4 = Unity.Mathematics.float4x4;
using quaternion = Unity.Mathematics.quaternion;

namespace DELTation.AAAARP.Lighting
{
    public static class AAAAShadowUtils
    {
        public static void ComputeDirectionalLightShadowMatrices(NativeArray<float3> cameraFrustumCorners, float3 cameraPosition, float cameraFarPlane,
            int resolution, quaternion lightRotation, float splitNear, float splitFar,
            out float4x4 lightView, out float4x4 lightProjection)
        {
            // From Wicked Engine: https://github.com/turanszkij/WickedEngine/blob/84adc794752a4b12d2551fef383d4872726a9255/WickedEngine/wiRenderer.cpp#L2735
            lightView = ConstructLightView(lightRotation);

            float perspectiveNearPlane = distance(cameraFrustumCorners[0], cameraPosition);
            float perspectiveFarPlane = distance(cameraFrustumCorners[1], cameraPosition);
            float splitNearNormalized = (splitNear - perspectiveNearPlane) / (perspectiveFarPlane - perspectiveNearPlane);
            float splitFarNormalized = (splitFar - perspectiveNearPlane) / (perspectiveFarPlane - perspectiveNearPlane);

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
            extrusion = max(extrusion, min(1500.0f, cameraFarPlane) * 0.5f);
            aabbMin.z = center.z - extrusion;
            aabbMax.z = center.z + extrusion;

            lightProjection = float4x4.OrthoOffCenter(aabbMin.x, aabbMax.x, aabbMin.y, aabbMax.y, aabbMin.z, aabbMax.z);
        }

        public static void ComputeSpotLightShadowMatrices(quaternion lightRotation, float3 lightPosition, float outerSpotAngle, float nearPlane, float farPlane,
            out float4x4 lightView, out float4x4 lightProjection
        )
        {
            lightView = float4x4.TRS(lightPosition, lightRotation, new float3(1, 1, 1));
            lightView = fastinverse(lightView);
            lightView = ToGPUView(lightView);

            const float aspect = 1.0f;
            lightProjection = float4x4.PerspectiveFov(radians(outerSpotAngle), aspect, nearPlane, farPlane);
        }

        private static float4x4 ConstructLightView(quaternion lightRotation)
        {
            quaternion invLightRotation = inverse(lightRotation);
            float3 lightForward = normalize(rotate(invLightRotation, float3(0, 0, 1)));
            float3 lightUp = normalize(rotate(invLightRotation, float3(0, 1, 0)));
            float4x4 lightView = LookRotation(lightForward, lightUp);

            return ToGPUView(lightView);

            static float4x4 LookRotation(float3 forward, float3 up)
            {
                var rot = float3x3.LookRotation(forward, up);

                float4x4 matrix;
                matrix.c0 = float4(rot.c0, 0.0f);
                matrix.c1 = float4(rot.c1, 0.0f);
                matrix.c2 = float4(rot.c2, 0.0f);
                matrix.c3 = float4(0.0f, 0.0f, 0.0f, 1.0f);
                return matrix;
            }
        }

        /// Unity view space Z is reversed.
        private static float4x4 ToGPUView(float4x4 viewMatrix) => mul(float4x4.Scale(float3(1, 1, -1)), viewMatrix);

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

        public static void GetScaleAndBiasForLinearDistanceFade(float fadeDistance, float border, out float scale, out float bias)
        {
            // To avoid division from zero
            // This values ensure that fade within cascade will be 0 and outside 1
            if (border < 0.0001f)
            {
                const float multiplier = 1000f; // To avoid blending if difference is in fractions
                scale = multiplier;
                bias = -fadeDistance * multiplier;
                return;
            }

            border = 1 - border;
            border *= border;

            float distanceFadeNear = border * fadeDistance;
            scale = 1.0f / (fadeDistance - distanceFadeNear);
            bias = -distanceFadeNear / (fadeDistance - distanceFadeNear);
        }

        public static void ComputePointLightShadowMatrices(float3 lightPosition, float nearPlane, float farPlane, int faceIndex,
            out float4x4 lightView, out float4x4 lightProjection, out TetrahedronFace tetrahedronFace)
        {
            tetrahedronFace = TetrahedronFace.Get(faceIndex);

            var faceRotation = quaternion.LookRotation(tetrahedronFace.Forward, tetrahedronFace.Up);
            lightView = float4x4.TRS(lightPosition, faceRotation, float3(1, 1, 1));
            lightView = fastinverse(lightView);
            lightView = ToGPUView(lightView);

            const float aspect = 1.0f;

            // FOV is a handpicked value.
            const float fov = 150.0f;
            lightProjection = float4x4.PerspectiveFov(radians(fov), aspect, nearPlane, farPlane);
        }

        public struct TetrahedronFace
        {
            private static readonly TetrahedronFace[] Faces =
            {
                new(float3(0.0f, 0.816497f, -0.57735f), float3(0, 1, 0)),
                new(float3(-0.816497f, 0.0f, 0.57735f), float3(0, 1, 0)),
                new(float3(0.816497f, 0.0f, 0.57735f), float3(0, 1, 0)),
                new(float3(0.0f, -0.816497f, -0.57735f), float3(0, -1, 0)),
            };

            public const int Count = 4;

            public TetrahedronFace(float3 forward, float3 up)
            {
                Forward = normalize(forward);
                Right = normalize(cross(forward, normalize(up)));
                Up = normalize(cross(Right, Forward));
            }

            public float3 Forward;
            public float3 Right;
            public float3 Up;

            public static ref readonly TetrahedronFace Get(int faceIndex) => ref Faces[faceIndex];
        }
    }
}