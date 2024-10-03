#ifndef AAAA_CAMERA_DEPTH_INCLUDED
#define AAAA_CAMERA_DEPTH_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"

TEXTURE2D(_CameraDepth);
SAMPLER(sampler_CameraDepth);

float LoadDeviceDepth(const uint2 screenCoords)
{
    return LOAD_TEXTURE2D(_CameraDepth, screenCoords);
}

float SampleDeviceDepth(const float2 screenUV)
{
    return SAMPLE_TEXTURE2D_LOD(_CameraDepth, sampler_CameraDepth, screenUV, 0).r;
}

float SampleLinearDepth(const float2 screenUV)
{
    const float deviceDepth = SampleDeviceDepth(screenUV);
    return Linear01Depth(deviceDepth, _ZBufferParams);
}

#endif // AAAA_CAMERA_DEPTH_INCLUDED