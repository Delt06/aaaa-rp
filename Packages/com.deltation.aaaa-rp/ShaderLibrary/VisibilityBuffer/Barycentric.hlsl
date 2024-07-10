#ifndef AAAA_VISIBILITY_BUFFER_BARYCENTRIC_INCLUDED
#define AAAA_VISIBILITY_BUFFER_BARYCENTRIC_INCLUDED

struct BarycentricDerivatives
{
    float3 lambda;
    float3 ddx;
    float3 ddy;
};

// http://filmicworlds.com/blog/visibility-buffer-rendering-with-material-graphs/
BarycentricDerivatives CalculateFullBarycentric(const float4 pt0, const float4 pt1, const float4 pt2, const float2 pixelNDC, const float2 invWinSize)
{
    BarycentricDerivatives ret = (BarycentricDerivatives)0;

    float3 invW = rcp(float3(pt0.w, pt1.w, pt2.w));

    float2 ndc0 = pt0.xy * invW.x;
    float2 ndc1 = pt1.xy * invW.y;
    float2 ndc2 = pt2.xy * invW.z;

    const float invDet = rcp(determinant(float2x2(ndc2 - ndc1, ndc0 - ndc1)));
    ret.ddx = float3(ndc1.y - ndc2.y, ndc2.y - ndc0.y, ndc0.y - ndc1.y) * invDet * invW;
    ret.ddy = float3(ndc2.x - ndc1.x, ndc0.x - ndc2.x, ndc1.x - ndc0.x) * invDet * invW;
    float ddxSum = dot(ret.ddx, float3(1, 1, 1));
    float ddySum = dot(ret.ddy, float3(1, 1, 1));

    float2      deltaVec = pixelNDC - ndc0;
    const float interpInvW = invW.x + deltaVec.x * ddxSum + deltaVec.y * ddySum;
    const float interpW = rcp(interpInvW);

    ret.lambda.x = interpW * (invW[0] + deltaVec.x * ret.ddx.x + deltaVec.y * ret.ddy.x);
    ret.lambda.y = interpW * (0.0f + deltaVec.x * ret.ddx.y + deltaVec.y * ret.ddy.y);
    ret.lambda.z = interpW * (0.0f + deltaVec.x * ret.ddx.z + deltaVec.y * ret.ddy.z);

    ret.ddx *= 2.0f * invWinSize.x;
    ret.ddy *= 2.0f * invWinSize.y;
    ddxSum *= 2.0f * invWinSize.x;
    ddySum *= 2.0f * invWinSize.y;

    // #if UNITY_UV_STARTS_AT_TOP
    // ret.ddy *= -1.0f;
    // ddySum    *= -1.0f;
    // #endif

    const float interpW_ddx = 1.0f / (interpInvW + ddxSum);
    const float interpW_ddy = 1.0f / (interpInvW + ddySum);

    ret.ddx = interpW_ddx * (ret.lambda * interpInvW + ret.ddx) - ret.lambda;
    ret.ddy = interpW_ddy * (ret.lambda * interpInvW + ret.ddy) - ret.lambda;

    return ret;
}

float3 InterpolateWithBarycentric(const BarycentricDerivatives barycentric, float v0, float v1, float v2)
{
    const float3 mergedV = float3(v0, v1, v2);
    float3       ret;
    ret.x = dot(mergedV, barycentric.lambda);
    ret.y = dot(mergedV, barycentric.ddx);
    ret.z = dot(mergedV, barycentric.ddy);
    return ret;
}

float3 InterpolateWithBarycentricNoDerivatives(const BarycentricDerivatives barycentric, float3 v0, float3 v1, float3 v2)
{
    return float3(
        InterpolateWithBarycentric(barycentric, v0.x, v1.x, v2.x).x,
        InterpolateWithBarycentric(barycentric, v0.y, v1.y, v2.y).x,
        InterpolateWithBarycentric(barycentric, v0.z, v1.z, v2.z).x
    );
}

#endif // AAAA_VISIBILITY_BUFFER_UTILS_INCLUDED