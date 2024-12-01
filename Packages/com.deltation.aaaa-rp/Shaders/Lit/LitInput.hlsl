#ifndef AAAA_LIT_INPUT_INCLUDED
#define AAAA_LIT_INPUT_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/SurfaceData.hlsl"
#include "Packages/com.deltation.aaaa-rp/Runtime/AAAAStructs.cs.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _BaseMap_ST;
    float4 _EmissionColor;
CBUFFER_END

float2 TransformBaseMapUV(const float2 uv)
{
    return uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
}

void InitSurfaceData(inout SurfaceData surfaceData, const float2 uv, const float3 normalWS)
{
    const float4 baseMapSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
    surfaceData.albedo = baseMapSample.rgb * _BaseColor.rgb;
    surfaceData.alpha = baseMapSample.a * _BaseColor.a;
    surfaceData.emission = _EmissionColor * baseMapSample.rgb;

    surfaceData.roughness = 0.5f;
    surfaceData.metallic = 0.0f;

    surfaceData.normalWS = SafeNormalize(normalWS);
}

#endif // AAAA_LIT_INPUT_INCLUDED