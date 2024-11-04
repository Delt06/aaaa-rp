//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef AAAASHADOWRENDERINGCONSTANTBUFFER_CS_HLSL
#define AAAASHADOWRENDERINGCONSTANTBUFFER_CS_HLSL
// Generated from DELTation.AAAARP.Lighting.AAAAShadowRenderingConstantBuffer
// PackingRules = Exact
CBUFFER_START(AAAAShadowRenderingConstantBuffer)
    float4x4 ShadowProjectionMatrix;
    float4x4 ShadowViewMatrix;
    float4x4 ShadowViewProjection;
    float4 ShadowLightDirection;
    float4 ShadowBiases;
CBUFFER_END


#endif
