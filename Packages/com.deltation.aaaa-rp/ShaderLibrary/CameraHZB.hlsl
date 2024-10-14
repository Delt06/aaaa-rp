#ifndef AAAA_CAMERA_HZB_INCLUDED
#define AAAA_CAMERA_HZB_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/CameraDepth.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Depth.hlsl"
#include "Packages/com.deltation.aaaa-rp/Runtime/Meshlets/AAAAMeshletComputeShaders.cs.hlsl"

float  _CameraHZBLevelCount;
float4 _CameraHZBMipRects[HZBMAX_LEVEL_COUNT];

TEXTURE2D_FLOAT(_CameraHZB);

struct CameraHZB
{
    static float Load(const int2 pixelCoord, const int lod)
    {
        if (lod == 0)
        {
            return LOAD_TEXTURE2D(_CameraDepth, pixelCoord).r;
        }

        int2 mipCoord = pixelCoord.xy >> lod;
        int2 mipOffset = _CameraHZBMipRects[lod].xy;
        return LOAD_TEXTURE2D(_CameraHZB, mipCoord + mipOffset).r;
    }

    static float LoadClamp(const int2 pixelCoord, const int lod)
    {
        float result;
        
        if (lod == 0)
        {
            result = LOAD_TEXTURE2D(_CameraDepth, clamp(pixelCoord, 0, _CameraHZBMipRects[0].zw - 1)).r;
        }
        else
        {
            int2 mipCoord = clamp(pixelCoord.xy >> lod, 0, _CameraHZBMipRects[lod].zw - 1);
            int2 mipOffset = _CameraHZBMipRects[lod].xy;
            result =  LOAD_TEXTURE2D(_CameraHZB, mipCoord + mipOffset).r;
        }

        return result;
    }

    static float LoadClampMipCoord(int2 mipCoord, const int lod)
    {
        float result;

        mipCoord = clamp(mipCoord, 0, _CameraHZBMipRects[lod].zw - 1);
        
        if (lod == 0)
        {
            result = LOAD_TEXTURE2D(_CameraDepth, mipCoord).r;
        }
        else
        {
            int2 mipOffset = _CameraHZBMipRects[lod].xy;
            result =  LOAD_TEXTURE2D(_CameraHZB, mipCoord + mipOffset).r;
        }

        return result;
    }

    static float LoadPadBorders(const int2 pixelCoord, const int lod)
    {
        int2 mipCoord = pixelCoord.xy >> lod;
        int2 mipSize = _CameraHZBMipRects[lod].zw;
        if (any(mipCoord <= 0) || any(mipCoord >= mipSize - 1))
        {
            return DEPTH_FAR;
        }

        if (lod == 0)
        {
            return LOAD_TEXTURE2D(_CameraDepth, pixelCoord).r;
        }
        int2 mipOffset = _CameraHZBMipRects[lod].xy;
        return LOAD_TEXTURE2D(_CameraHZB, mipCoord + mipOffset).r;
    }
};

#endif // AAAA_CAMERA_HZB_INCLUDED