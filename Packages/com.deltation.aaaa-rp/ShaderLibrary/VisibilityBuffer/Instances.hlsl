#ifndef AAAA_VISIBILITY_BUFFER_INSTANCES_INCLUDED
#define AAAA_VISIBILITY_BUFFER_INSTANCES_INCLUDED

#include "Packages/com.deltation.aaaa-rp/Runtime/AAAAStructs.cs.hlsl"

StructuredBuffer<AAAAInstanceData> _InstanceData;
uint                               _InstanceCount;

AAAAInstanceData PullInstanceData(const uint instanceID)
{
    return _InstanceData[instanceID];
}

#endif // AAAA_VISIBILITY_BUFFER_INSTANCES_INCLUDED