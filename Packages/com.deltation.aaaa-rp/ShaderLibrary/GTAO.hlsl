#ifndef AAAA_GTAO_INCLUDED
#define AAAA_GTAO_INCLUDED

Texture2D<uint> _GTAOTerm;

#if defined(AAAA_GTAO) || defined(AAAA_GTAO_BENT_NORMALS)
#define AAAA_GTAO_ANY 1
#define MULTI_BOUNCE_AMBIENT_OCCLUSION 1
#endif

struct GTAOUtils
{
    static float4 R8G8B8A8_UNORM_to_FLOAT4(uint packedInput)
    {
        float4 unpackedOutput;
        unpackedOutput.x = (float)(packedInput & 0x000000ff) / 255;
        unpackedOutput.y = (float)(((packedInput >> 8) & 0x000000ff)) / 255;
        unpackedOutput.z = (float)(((packedInput >> 16) & 0x000000ff)) / 255;
        unpackedOutput.w = (float)(packedInput >> 24) / 255;
        return unpackedOutput;
    }

    static float SpecularAO_Lagarde(float NoV, float visibility, float roughness)
    {
        // Lagarde and de Rousiers 2014, "Moving Frostbite to PBR"
        return saturate(pow(NoV + visibility, exp2(-16.0 * roughness - 1.0)) - 1.0 + visibility);
    }

    // This function could (should?) be implemented as a 3D LUT instead, but we need to save samplers
    static float SpecularAO_Cones(float visibility, const float3 bentNormalWS, const float3 R, const float NoV, float roughness)
    {
        #ifdef AAAA_GTAO_ANY_BENT_NORMALS
        // Jimenez et al. 2016, "Practical Realtime Strategies for Accurate Indirect Occlusion"

        // aperture from ambient occlusion
        float cosAv = sqrt(1.0 - visibility);
        // aperture from roughness, log(10) / log(2) = 3.321928
        float cosAs = exp2(-3.321928 * Sq(roughness));
        // angle betwen bent normal and reflection direction
        float cosB  = dot(bentNormalWS, R);

        // Remove the 2 * PI term from the denominator, it cancels out the same term from
        // sphericalCapsIntersection()
        float ao = GTAOUtils::SphericalCapsIntersection(cosAv, cosAs, cosB) / (1.0 - cosAs);
        // Smoothly kill specular AO when entering the perceptual roughness range [0.1..0.3]
        // Without this, specular AO can remove all reflections, which looks bad on metals
        return lerp(1.0, ao, smoothstep(0.01, 0.09, roughness));
        #else
        return GTAOUtils::SpecularAO_Lagarde(NoV, visibility, roughness);
        #endif
    }

    //
    static float FastSqrt(float x)
    {
        // http://h14s.p5r.org/2012/09/0x5f3759df.html, [Drobot2014a] Low Level Optimizations for GCN
        // https://blog.selfshadow.com/publications/s2016-shading-course/activision/s2016_pbs_activision_occlusion.pdf slide 63
        return asfloat(0x1fbd1df5 + (asint(x) >> 1));
    }

    //
    // From https://seblagarde.wordpress.com/2014/12/01/inverse-trigonometric-functions-gpu-optimization-for-amd-gcn-architecture/
    // input [-1, 1] and output [0, PI]
    static float FastAcos(float inX)
    {
        float x = abs(inX);
        float res = -0.156583f * x + PI * 0.5;
        res *= FastSqrt(1.0f - x); // consider using normal sqrt here?
        return (inX >= 0) ? res : PI - res;
    }

    //
    // Approximates acos(x) with a max absolute error of 9.0x10^-3.
    // Input [0, 1]
    static float FastAcosPositive(float x)
    {
        float p = -0.1565827f * x + 1.570796f;
        return p * sqrt(1.0 - x);
    }

    static float SphericalCapsIntersection(float cosCap1, float cosCap2, float cosDistance)
    {
        // Oat and Sander 2007, "Ambient Aperture Lighting"
        // Approximation mentioned by Jimenez et al. 2016
        float r1 = FastAcosPositive(cosCap1);
        float r2 = FastAcosPositive(cosCap2);
        float d = FastAcos(cosDistance);

        // We work with cosine angles, replace the original paper's use of
        // cos(min(r1, r2)) with max(cosCap1, cosCap2)
        // We also remove a multiplication by 2 * PI to simplify the computation
        // since we divide by 2 * PI in computeBentSpecularAO()

        if (min(r1, r2) <= max(r1, r2) - d)
        {
            return 1.0 - max(cosCap1, cosCap2);
        }
        if (r1 + r2 <= d)
        {
            return 0.0;
        }

        float delta = abs(r1 - r2);
        float x = 1.0 - saturate((d - delta) / max(r1 + r2 - delta, 1e-4));
        // simplified smoothstep()
        float area = Sq(x) * (-2.0 * x + 3.0);
        return area * (1.0 - max(cosCap1, cosCap2));
    }
};


void DecodeVisibilityBentNormal(const uint packedValue, out float visibility, out float3 bentNormal)
{
    float4 decoded = GTAOUtils::R8G8B8A8_UNORM_to_FLOAT4(packedValue);
    bentNormal = decoded.xyz * 2.0 - 1.0; // could normalize - don't want to since it's done so many times, better to do it at the final step only
    visibility = decoded.w;
}

void SampleGTAO(const uint2 pixelCoords, const float3 normalWS, out float visibility, out float3 bentNormalWS)
{
    visibility = 1;
    bentNormalWS = normalWS;

    #ifdef AAAA_GTAO_ANY
    const uint packedValue = LOAD_TEXTURE2D(_GTAOTerm, pixelCoords).r;
    #ifdef AAAA_GTAO_BENT_NORMALS
    DecodeVisibilityBentNormal(packedValue, visibility, bentNormalWS);
    bentNormalWS = TransformViewToWorldNormal(bentNormalWS, true);
    #else
    visibility = packedValue / 255.05;
    #endif
    #endif
}

float SingleBounceAO(float visibility)
{
    #ifdef AAAA_GTAO_ANY
    #if MULTI_BOUNCE_AMBIENT_OCCLUSION == 1
    return 1.0;
    #else
    return visibility;
    #endif
    #else
    return 1.0;
    #endif
}

/**
 * Computes a specular occlusion term from the ambient occlusion term.
 */
float ComputeSpecularAO(float visibility, const float3 bentNormalWS, const float3 R, const float NoV, float roughness)
{
    #if defined(AAAA_GTAO)
    return GTAOUtils::SpecularAO_Lagarde(NoV, visibility, roughness);
    #elif defined(AAAA_GTAO_BENT_NORMALS)
    return GTAOUtils::SpecularAO_Cones(visibility, bentNormalWS, R, NoV, roughness);
    #else
    return 1.0;
    #endif
}

float3 GtaoMultiBounce(float visibility, const float3 albedo)
{
    // Jimenez et al. 2016, "Practical Realtime Strategies for Accurate Indirect Occlusion"
    float3 a = 2.0404 * albedo - 0.3324;
    float3 b = -4.7951 * albedo + 0.6417;
    float3 c = 2.7552 * albedo + 0.6903;

    return max(float3(visibility.xxx), ((visibility * a + b) * visibility + c) * visibility);
}

void MultiBounceAO(float visibility, const float3 albedo, inout float3 color)
{
    #if MULTI_BOUNCE_AMBIENT_OCCLUSION
    color *= GtaoMultiBounce(visibility, albedo);
    #endif
}

void MultiBounceSpecularAO(float visibility, const float3 albedo, inout float3 color)
{
    #if MULTI_BOUNCE_AMBIENT_OCCLUSION
    color *= GtaoMultiBounce(visibility, albedo);
    #endif
}

#endif // AAAA_GTAO_INCLUDED