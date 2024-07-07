#ifndef AAAA_INPUT_INCLUDED
#define AAAA_INPUT_INCLUDED
// AAAA package specific shader input variables and defines.
// Unity Engine specific built-in shader input variables are defined in ShaderLibrary/UnityInput.hlsl

///////////////////////////////////////////////////////////////////////////////
//                      Constant Buffers                                     //
///////////////////////////////////////////////////////////////////////////////

half4 _GlossyEnvironmentColor;
half4 _SubtractiveShadowColor;

half4 _GlossyEnvironmentCubeMap_HDR;
TEXTURECUBE(_GlossyEnvironmentCubeMap);
SAMPLER(sampler_GlossyEnvironmentCubeMap);

#define _InvCameraViewProj unity_MatrixInvVP
float4 _ScaledScreenParams;

// x = Mip Bias
// y = 2.0 ^ [Mip Bias]
float2 _GlobalMipBias;

half4 _AdditionalLightsCount;

uint _RenderingLayerMaxInt;
float _RenderingLayerRcpMaxInt;

// Screen coord override.
float4 _ScreenCoordScaleBias;
float4 _ScreenSizeOverride;

uint _EnableProbeVolumes;

#define UNITY_MATRIX_M     unity_ObjectToWorld
#define UNITY_MATRIX_I_M   unity_WorldToObject
#define UNITY_MATRIX_V     unity_MatrixV
#define UNITY_MATRIX_I_V   unity_MatrixInvV
#define UNITY_MATRIX_P     OptimizeProjectionMatrix(glstate_matrix_projection)
#define UNITY_MATRIX_I_P   unity_MatrixInvP
#define UNITY_MATRIX_VP    unity_MatrixVP
#define UNITY_MATRIX_I_VP  unity_MatrixInvVP
#define UNITY_MATRIX_MV    mul(UNITY_MATRIX_V, UNITY_MATRIX_M)
#define UNITY_MATRIX_T_MV  transpose(UNITY_MATRIX_MV)
#define UNITY_MATRIX_IT_MV transpose(mul(UNITY_MATRIX_I_M, UNITY_MATRIX_I_V))
#define UNITY_MATRIX_MVP   mul(UNITY_MATRIX_VP, UNITY_MATRIX_M)
#define UNITY_PREV_MATRIX_M   unity_MatrixPreviousM
#define UNITY_PREV_MATRIX_I_M unity_MatrixPreviousMI

// Note: #include order is important here.
// UnityInput.hlsl must be included before UnityInstancing.hlsl, so constant buffer
// declarations don't fail because of instancing macros.
// UniversalDOTSInstancing.hlsl must be included after UnityInstancing.hlsl
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/UnityInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
// #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/UniversalDOTSInstancing.hlsl"

// VFX may also redefine UNITY_MATRIX_M / UNITY_MATRIX_I_M as static per-particle global matrices.
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.visualeffectgraph/Shaders/VFXMatricesOverride.hlsl"
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

#endif // AAAA_INPUT_INCLUDED
