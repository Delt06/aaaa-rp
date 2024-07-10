#ifndef AAAA_LIGHTING_INCLUDED
#define AAAA_LIGHTING_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"

float4 _MainLight_Color;
float4 _MainLight_Direction;

struct Light
{
    float3 color;
    float3 direction;
};

Light GetMainLight()
{
    Light light;
    light.color = _MainLight_Color.rgb;
    light.direction = _MainLight_Direction.xyz;
    return light;
}

#endif // AAAA_GBUFFER_INCLUDED