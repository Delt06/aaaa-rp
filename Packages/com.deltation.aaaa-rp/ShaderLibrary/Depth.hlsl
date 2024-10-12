#ifndef AAAA_DEPTH_INCLUDED
#define AAAA_DEPTH_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

#if UNITY_REVERSED_Z
#define DEPTH_FAR 0
#define MIN_DEPTH(l, r) (max((l), (r)))
#define MAX_DEPTH(l, r) (min((l), (r)))
#define LESS_DEPTH(l, r) (l > r)
#define LEQUAL_DEPTH(l, r) (l >= r)
#define GREATER_DEPTH(l, r) (l < r)
#define GEQUAL_DEPTH(l, r) (l <= r)
#else
#define DEPTH_FAR 1
#define MIN_DEPTH(l, r) (min((l), (r)))
#define MAX_DEPTH(l, r) (max((l), (r)))
#define LESS_DEPTH(l, r) (l < r)
#define LEQUAL_DEPTH(l, r) (l <= r)
#define GREATER_DEPTH(l, r) (l > r)
#define GEQUAL_DEPTH(l, r) (l >= r)
#endif

#endif // AAAA_DEPTH_INCLUDED