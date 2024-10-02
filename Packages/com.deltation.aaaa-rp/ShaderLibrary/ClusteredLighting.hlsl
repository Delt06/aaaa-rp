#ifndef AAAA_CLUSTERED_LIGHTING_INCLUDED
#define AAAA_CLUSTERED_LIGHTING_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/PunctualLights.hlsl"
#include "Packages/com.deltation.aaaa-rp/Shaders/ClusteredLighting/Common.hlsl"

StructuredBuffer<AAAAClusteredLightingGridCell> _ClusteredLightGrid;
ByteAddressBuffer                               _ClusteredLightIndexList;

struct ClusteredLighting
{
    static uint NormalizedScreenUVToFlatClusterIndex(const float2 screenUV, const float zVS)
    {
        uint3 clusterIndex;
        clusterIndex.x = clamp(screenUV.x * CLUSTERS_X, 0, CLUSTERS_X - 1);
        clusterIndex.y = clamp(screenUV.y * CLUSTERS_Y, 0, CLUSTERS_Y - 1);
        clusterIndex.z = clamp(ClusteredLightingCommon::ViewSpaceZToClusterIndex(zVS), 0, CLUSTERS_Z - 1);
        return ClusteredLightingCommon::FlattenClusterIndex(clusterIndex);
    }

    static AAAAClusteredLightingGridCell LoadCell(const uint flatClusterIndex)
    {
        return _ClusteredLightGrid[flatClusterIndex];
    }

    static AAAAClusteredLightingGridCell LoadCell(const float3 positionWS, const float2 screenUV)
    {
        const float zVS = TransformWorldToView(positionWS).z;
        const uint  flatClusterIndex = NormalizedScreenUVToFlatClusterIndex(screenUV, zVS);
        return LoadCell(flatClusterIndex);
    }

    static uint LoadLightIndex(const AAAAClusteredLightingGridCell lightGridCell, const uint localIndex)
    {
        return _ClusteredLightIndexList.Load(4 * (lightGridCell.Offset + localIndex));
    }
};


#endif // AAAA_CLUSTERED_LIGHTING_INCLUDED