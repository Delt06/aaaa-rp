//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef XEGTAO_H_CS_HLSL
#define XEGTAO_H_CS_HLSL
// Generated from DELTation.AAAARP.ShaderLibrary.ThirdParty.XeGTAO.XeGTAO+GTAOConstantsCS
// PackingRules = Exact
CBUFFER_START(GTAOConstantsCS)
    int2 ViewportSize;
    float2 ViewportPixelSize;
    float2 DepthUnpackConsts;
    float2 CameraTanHalfFOV;
    float2 NDCToViewMul;
    float2 NDCToViewAdd;
    float2 NDCToViewMul_x_PixelSize;
    float EffectRadius;
    float EffectFalloffRange;
    float RadiusMultiplier;
    float Padding0;
    float FinalValuePower;
    float DenoiseBlurBeta;
    float SampleDistributionPower;
    float ThinOccluderCompensation;
    float DepthMIPSamplingOffset;
    int NoiseIndex;
CBUFFER_END


#endif
