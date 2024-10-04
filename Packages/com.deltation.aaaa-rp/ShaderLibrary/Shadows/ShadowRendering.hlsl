#ifndef AAAA_SHADOW_RENDERING_INCLUDED
#define AAAA_SHADOW_RENDERING_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.deltation.aaaa-rp/Runtime/Lighting/AAAAShadowRenderingConstantBuffer.cs.hlsl"

#define UNITY_MATRIX_P (ShadowProjectionMatrix)
#define UNITY_MATRIX_V (ShadowViewMatrix)
#define UNITY_MATRIX_VP (ShadowViewProjection)

#endif // AAAA_SHADOW_RENDERING_INCLUDED