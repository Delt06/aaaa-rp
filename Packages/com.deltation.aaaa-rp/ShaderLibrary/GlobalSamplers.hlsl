#ifndef AAAA_GLOBAL_SAMPLERS_INCLUDED
#define AAAA_GLOBAL_SAMPLERS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

SAMPLER(sampler_TrilinearRepeat_Aniso16);
SAMPLER_CMP(sampler_LinearClampCompare);

#endif // AAAA_GLOBAL_SAMPLERS_INCLUDED