#ifndef AAAA_PUNCTUAL_LIGHTS_INCLUDED
#define AAAA_PUNCTUAL_LIGHTS_INCLUDED

#include "Packages/com.deltation.aaaa-rp/Runtime/Lighting/AAAALightingConstantBuffer.cs.hlsl"
#include "Packages/com.deltation.aaaa-rp/Runtime/Lighting/AAAAPunctualLightData.cs.hlsl"

StructuredBuffer<AAAAPunctualLightData> _PunctualLights;

// https://github.com/Unity-Technologies/Graphics/blob/e42df452b62857a60944aed34f02efa1bda50018/Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl#L47
float DistanceAttenuation(const float distanceSqr, const float distanceAttenuation)
{
    const float lightAttenuation = rcp(distanceSqr);
    
    // Use the smoothing factor also used in the Unity lightmapper.
    const float factor = float(distanceSqr * distanceAttenuation);
    float smoothFactor = saturate(1.0 - factor * factor);
    smoothFactor = smoothFactor * smoothFactor;

    return lightAttenuation * smoothFactor;
}

#endif // AAAA_GBUFFER_INCLUDED