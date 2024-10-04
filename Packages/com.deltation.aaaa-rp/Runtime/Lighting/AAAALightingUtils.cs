using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace DELTation.AAAARP.Lighting
{
    public static class AAAALightingUtils
    {
        public static float3 ExtractDirection(Matrix4x4 localToWorldMatrix) =>
            -((float4) localToWorldMatrix.GetColumn(2)).xyz;

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

        // https://github.com/Unity-Technologies/Graphics/blob/e42df452b62857a60944aed34f02efa1bda50018/Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/Light/HDGpuLightsBuilder.LightLoop.cs#L925
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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