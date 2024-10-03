#ifndef AAAA_CLUSTERED_LIGHTING_INCLUDED
#define AAAA_CLUSTERED_LIGHTING_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/PunctualLights.hlsl"
#include "Packages/com.deltation.aaaa-rp/Shaders/ClusteredLighting/Common.hlsl"

StructuredBuffer<AAAAClusteredLightingGridCell> _ClusteredLightGrid;
ByteAddressBuffer                               _ClusteredLightIndexList;

struct ClusteredLighting
{
    static AAAAClusteredLightingGridCell LoadCell(const uint flatClusterIndex)
    {
        return _ClusteredLightGrid[flatClusterIndex];
    }

    static AAAAClusteredLightingGridCell LoadCell(const float3 positionWS, const float2 screenUV)
    {
        const float zVS = TransformWorldToView(positionWS).z;
        const uint  flatClusterIndex = ClusteredLightingCommon::NormalizedScreenUVToFlatClusterIndex(screenUV, zVS);
        return LoadCell(flatClusterIndex);
    }

    static uint LoadLightIndex(const AAAAClusteredLightingGridCell lightGridCell, const uint localIndex)
    {
        return _ClusteredLightIndexList.Load(4 * (lightGridCell.Offset + localIndex));
    }
};


#endif // AAAA_CLUSTERED_LIGHTING_INCLUDED