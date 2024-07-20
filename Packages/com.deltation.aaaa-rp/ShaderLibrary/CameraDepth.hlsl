#ifndef AAAA_CAMERA_DEPTH_INCLUDED
#define AAAA_CAMERA_DEPTH_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"

TEXTURE2D(_CameraDepth);
SAMPLER(sampler_CameraDepth);

float SampleLinearDepth(const float2 screenUV)
{
    const float deviceDepth = SAMPLE_TEXTURE2D_LOD(_CameraDepth, sampler_CameraDepth, screenUV, 0).r;
    return Linear01Depth(deviceDepth, _ZBufferParams);
}

#endif // AAAA_CAMERA_DEPTH_INCLUDED