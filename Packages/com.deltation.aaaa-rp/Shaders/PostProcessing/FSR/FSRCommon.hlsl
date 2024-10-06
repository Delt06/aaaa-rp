#ifndef AAAA_FSR_COMMON_INCLUDED
#define AAAA_FSR_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "AAAAFSRConstantBuffer.cs.hlsl"

#define A_GPU 1
#define A_HLSL 1

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/ThirdParty/FidelityFX/FSR/ffx_a.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/ThirdParty/FidelityFX/FSR/ffx_fsr1.hlsl"

void CurrFilter(int2 pos);

[numthreads(64, 1, 1)]
void mainCS(uint3 LocalThreadId : SV_GroupThreadID, uint3 WorkGroupId : SV_GroupID)
{
    // Do remapping of local xy in workgroup for a more PS-like swizzle pattern.
    AU2 gxy = ARmp8x8(LocalThreadId.x) + AU2(WorkGroupId.x << 4u, WorkGroupId.y << 4u);
    CurrFilter(gxy);
    gxy.x += 8u;
    CurrFilter(gxy);
    gxy.y += 8u;
    CurrFilter(gxy);
    gxy.x -= 8u;
    CurrFilter(gxy);
}

#endif // AAAA_FSR_COMMON_INCLUDED