#ifndef AAAA_TONE_MAPPING_HLSL
#define AAAA_TONE_MAPPING_HLSL

struct ToneMapping
{
    static float3 ACES(const float3 x)
    {
        const float a = 2.51;
        const float b = 0.03;
        const float c = 2.43;
        const float d = 0.59;
        const float e = 0.14;
        return clamp((x * (a * x + b)) / (x * (c * x + d) + e), 0.0, 1.0);
    }

    // Khronos PBR Neutral Tone Mapper
    // https://github.com/KhronosGroup/ToneMapping/tree/main/PBR_Neutral

    // Input color is non-negative and resides in the Linear Rec. 709 color space.
    // Output color is also Linear Rec. 709, but in the [0, 1] range.
    static float3 Neutral(float3 color)
    {
        const float startCompression = 0.8 - 0.04;
        const float desaturation = 0.15;

        float x = min(color.r, min(color.g, color.b));
        float offset = x < 0.08 ? x - 6.25 * x * x : 0.04;
        color -= offset;

        float peak = max(color.r, max(color.g, color.b));
        if (peak < startCompression) return color;

        const float d = 1.0 - startCompression;
        float       newPeak = 1.0 - d * d / (peak + d - startCompression);
        color *= newPeak / peak;

        float g = 1.0 - 1.0 / (desaturation * (peak - newPeak) + 1.0);
        return lerp(color, newPeak.xxx, g);
    }
};

#endif // AAAA_TONE_MAPPING_HLSL