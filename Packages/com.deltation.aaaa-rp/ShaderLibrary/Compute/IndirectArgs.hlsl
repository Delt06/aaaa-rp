#ifndef AAAA_INDIRECT_ARGS_INCLUDED
#define AAAA_INDIRECT_ARGS_INCLUDED

#include <UnityIndirect.cginc>

#include "Packages/com.deltation.aaaa-rp/Runtime/AAAAStructs.cs.hlsl"

struct IndirectArgs
{
    static uint4 PackDispatchArgs(const IndirectDispatchArgs dispatchArgs)
    {
        uint4 value;
        value.x = dispatchArgs.ThreadGroupsX;
        value.y = dispatchArgs.ThreadGroupsY;
        value.z = dispatchArgs.ThreadGroupsZ;
        value.w = 0;
        return value;
    }

    static uint4 PackIndirectDrawArgs(const IndirectDrawArgs drawArgs)
    {
        uint4 value;
        value.x = drawArgs.vertexCountPerInstance;
        value.y = drawArgs.instanceCount;
        value.z = drawArgs.startVertex;
        value.w = drawArgs.startInstance;
        return value;
    }
};

#endif // AAAA_INDIRECT_ARGS_INCLUDED