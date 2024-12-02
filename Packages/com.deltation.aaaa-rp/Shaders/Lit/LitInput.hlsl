#ifndef AAAA_LIT_INPUT_INCLUDED
#define AAAA_LIT_INPUT_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/SurfaceData.hlsl"
#include "Packages/com.deltation.aaaa-rp/Runtime/AAAAStructs.cs.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

TEXTURE2D(_BumpMap);
SAMPLER(sampler_BumpMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _BaseMap_ST;
    float4 _EmissionColor;
    float  _AlphaClipThreshold;
    float  _BumpMapScale;
CBUFFER_END

float2 TransformBaseMapUV(const float2 uv)
{
    return uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
}

void InitSurfaceData(inout SurfaceData surfaceData, const float2 uv, const float3 normalWS, const float4 tangentWS, FRONT_FACE_TYPE face)
{
    const float4 baseMapSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
    surfaceData.albedo = baseMapSample.rgb * _BaseColor.rgb;
    surfaceData.alpha = baseMapSample.a * _BaseColor.a;
    surfaceData.emission = _EmissionColor.rgb * baseMapSample.rgb;

    surfaceData.roughness = 0.5f;
    surfaceData.metallic = 0.0f;

    surfaceData.normalWS = IS_FRONT_VFACE(face, 1, -1) * SafeNormalize(normalWS);

    const float3   bitangentWS = tangentWS.w * cross(surfaceData.normalWS, tangentWS.xyz);
    const float3x3 tangentToWorld = float3x3(tangentWS.xyz, bitangentWS, surfaceData.normalWS);

    const float4 packedNormal = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv);
    const float3 normalTS = UnpackNormalScale(packedNormal, _BumpMapScale);
    surfaceData.normalWS = TransformTangentToWorld(normalTS, tangentToWorld, true);
}

void AlphaClip(const SurfaceData surfaceData)
{
    #ifdef _ALPHATEST_ON
    clip(surfaceData.alpha - _AlphaClipThreshold);
    #endif
}

#endif // AAAA_LIT_INPUT_INCLUDED