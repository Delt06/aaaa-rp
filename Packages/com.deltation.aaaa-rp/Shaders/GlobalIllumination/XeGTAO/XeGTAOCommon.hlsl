#ifndef AAAA_XE_GTAO_COMMON_INCLUDED
#define AAAA_XE_GTAO_COMMON_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/GlobalSamplers.hlsl"

#define XE_GTAO_USE_HALF_FLOAT_PRECISION 0
#define VA_SATURATE(x) saturate(x) 
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/ThirdParty/XeGTAO/XeGTAO.h.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/ThirdParty/XeGTAO/XeGTAO.h.cs.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/ThirdParty/XeGTAO/XeGTAO.hlsl"

GTAOConstants LoadGTAOConstants()
{
    GTAOConstants gtaoConstants;
    
    gtaoConstants.ViewportSize = ViewportSize;
    gtaoConstants.ViewportPixelSize = ViewportPixelSize;
    gtaoConstants.DepthUnpackConsts = DepthUnpackConsts;
    gtaoConstants.CameraTanHalfFOV = CameraTanHalfFOV;
    gtaoConstants.NDCToViewMul = NDCToViewMul;
    gtaoConstants.NDCToViewAdd = NDCToViewAdd;
    gtaoConstants.NDCToViewMul_x_PixelSize = NDCToViewMul_x_PixelSize;
    gtaoConstants.EffectRadius = EffectRadius;
    gtaoConstants.EffectFalloffRange = EffectFalloffRange;
    gtaoConstants.RadiusMultiplier = RadiusMultiplier;
    gtaoConstants.Padding0 = Padding0;
    gtaoConstants.FinalValuePower = FinalValuePower;
    gtaoConstants.DenoiseBlurBeta = DenoiseBlurBeta;
    gtaoConstants.SampleDistributionPower = SampleDistributionPower;
    gtaoConstants.ThinOccluderCompensation = ThinOccluderCompensation;
    gtaoConstants.DepthMIPSamplingOffset = DepthMIPSamplingOffset;
    gtaoConstants.NoiseIndex = NoiseIndex;

    return gtaoConstants;
}

#endif // AAAA_XE_GTAO_COMMON_INCLUDED