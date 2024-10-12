#ifndef AAAA_CAMERA_DEPTH_INCLUDED
#define AAAA_CAMERA_DEPTH_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"

TYPED_TEXTURE2D(float, _CameraDepth);
SAMPLER(sampler_CameraDepth);

float SampleDeviceDepth(const float2 screenUV)
{
    return SAMPLE_TEXTURE2D_LOD(_CameraDepth, sampler_CameraDepth, screenUV, 0).r;
}

float SampleLinearDepth(const float2 screenUV)
{
    const float deviceDepth = SampleDeviceDepth(screenUV);
    return Linear01Depth(deviceDepth, _ZBufferParams);
}

float LoadDeviceDepth(const uint2 pixelCoords)
{
    return _CameraDepth[pixelCoords].r;
}

#endif // AAAA_CAMERA_DEPTH_INCLUDED