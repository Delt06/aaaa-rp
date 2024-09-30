#ifndef AAAA_CLUSTERED_LIGHTING_INCLUDED
#define AAAA_CLUSTERED_LIGHTING_INCLUDED

#include "Packages/com.deltation.aaaa-rp/Runtime/Passes/ClusteredLighting/AAAAClusteredLightingShaderData.cs.hlsl"

struct ClusteredLighting
{
    static uint3 UnflattenClusterIndex(const uint flatClusterIndex)
    {
        const uint z = flatClusterIndex / (CLUSTERS_X * CLUSTERS_Y);
        const uint xy = flatClusterIndex % (CLUSTERS_X * CLUSTERS_Y);
        const uint x = xy % CLUSTERS_X;
        const uint y = xy / CLUSTERS_X;
        return uint3(x, y, z);
    }

    static uint FlattenClusterIndex(const uint3 clusterIndex)
    {
        return clusterIndex.x + clusterIndex.y * CLUSTERS_X + clusterIndex.z * CLUSTERS_X * CLUSTERS_Y;
    }

    static float ClusterIndexToViewSpaceZ(const uint zClusterIndex)
    {
        const float zNear = _ProjectionParams.y;
        const float zFar = _ProjectionParams.z;
        return -zNear * pow(max(0, zFar / zNear), (float)zClusterIndex / CLUSTERS_Z);
    }

    static uint ViewSpaceZToClusterIndex(const float zVS)
    {
        const float zNear = _ProjectionParams.y;
        const float zFar = _ProjectionParams.z;
        return uint(max(log2(-zVS / zNear) / log2(zFar / zNear), 0) * CLUSTERS_Z);
    }
};

#endif // AAAA_CLUSTERED_LIGHTING_INCLUDED