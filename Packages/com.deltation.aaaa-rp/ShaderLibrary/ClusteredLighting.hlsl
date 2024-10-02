#ifndef AAAA_CLUSTERED_LIGHTING_INCLUDED
#define AAAA_CLUSTERED_LIGHTING_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/PunctualLights.hlsl"
#include "Packages/com.deltation.aaaa-rp/Shaders/ClusteredLighting/Common.hlsl"

ByteAddressBuffer _ClusteredLightGrid;
ByteAddressBuffer _ClusteredLightIndexList;

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

    static LightGridCell LoadCell(const uint flatClusterIndex)
    {
        const uint packedCell = _ClusteredLightGrid.Load(4 * flatClusterIndex);
        return LightGridCell::Unpack(packedCell);
    }

    static uint LoadLightIndex(const LightGridCell lightGridCell, const uint localIndex)
    {
        return _ClusteredLightIndexList.Load(4 * (lightGridCell.offset + localIndex));
    }
};


#endif // AAAA_CLUSTERED_LIGHTING_INCLUDED