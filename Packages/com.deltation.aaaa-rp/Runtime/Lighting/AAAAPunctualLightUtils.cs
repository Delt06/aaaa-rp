using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Lighting
{
    public static class AAAAPunctualLightUtils
    {
        // https://github.com/Unity-Technologies/Graphics/blob/e42df452b62857a60944aed34f02efa1bda50018/Packages/com.unity.render-pipelines.universal/Runtime/UniversalRenderPipelineCore.cs#L1732
        public static void GetPunctualLightDistanceAttenuation(in VisibleLight visibleLight, out float attenuation)
        {
            float lightRangeSqr = visibleLight.range * visibleLight.range;
            float oneOverLightRangeSqr = 1.0f / Mathf.Max(0.0001f, lightRangeSqr);

            attenuation = oneOverLightRangeSqr;
        }

        // https://github.com/Unity-Technologies/Graphics/blob/e42df452b62857a60944aed34f02efa1bda50018/Packages/com.unity.render-pipelines.universal/Runtime/UniversalRenderPipelineCore.cs#L1752
        public static void GetPunctualLightSpotAngleAttenuation(in VisibleLight visibleLight, float? innerSpotAngle, out float attenuationX,
            out float attenuationY)
        {
            if (visibleLight.lightType != LightType.Spot)
            {
                // Makes MAD always return 1
                attenuationX = 0;
                attenuationY = 1;
            }
            else
            {
                // Spot Attenuation with a linear falloff can be defined as
                // (SdotL - cosOuterAngle) / (cosInnerAngle - cosOuterAngle)
                // This can be rewritten as
                // invAngleRange = 1.0 / (cosInnerAngle - cosOuterAngle)
                // SdotL * invAngleRange + (-cosOuterAngle * invAngleRange)
                // If we precompute the terms in a MAD instruction
                float cosOuterAngle = Mathf.Cos(Mathf.Deg2Rad * visibleLight.spotAngle * 0.5f);

                // We need to do a null check for particle lights
                // This should be changed in the future
                // Particle lights will use an inline function
                float cosInnerAngle = innerSpotAngle.HasValue
                    ? Mathf.Cos(innerSpotAngle.Value * Mathf.Deg2Rad * 0.5f)
                    : Mathf.Cos(2.0f * Mathf.Atan(Mathf.Tan(visibleLight.spotAngle * 0.5f * Mathf.Deg2Rad) * (64.0f - 18.0f) / 64.0f) * 0.5f);
                float smoothAngleRange = Mathf.Max(0.001f, cosInnerAngle - cosOuterAngle);
                float invAngleRange = 1.0f / smoothAngleRange;
                float add = -cosOuterAngle * invAngleRange;

                attenuationX = invAngleRange;
                attenuationY = add;
            }
        }
    }
}