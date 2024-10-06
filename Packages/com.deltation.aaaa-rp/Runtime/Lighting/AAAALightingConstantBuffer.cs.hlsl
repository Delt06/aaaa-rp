//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef AAAALIGHTINGCONSTANTBUFFER_CS_HLSL
#define AAAALIGHTINGCONSTANTBUFFER_CS_HLSL
//
// DELTation.AAAARP.Lighting.AAAALightingConstantBuffer:  static fields
//
#define MAX_DIRECTIONAL_LIGHTS (4)

// Generated from DELTation.AAAARP.Lighting.AAAALightingConstantBuffer
// PackingRules = Exact
CBUFFER_START(AAAALightingConstantBuffer)
    float4 DirectionalLightColors[4];
    float4 DirectionalLightDirections[4];
    float4 DirectionalLightShadowSliceRanges_ShadowFadeParams[4];
    uint DirectionalLightCount;
    uint PunctualLightCount;
CBUFFER_END


#endif
