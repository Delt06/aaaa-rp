#ifndef AAAA_MESHLET_CULLING_INCLUDED
#define AAAA_MESHLET_CULLING_INCLUDED

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Meshlets.hlsl"

bool ConeCulling(const float3           cameraPositionWS, const float3  viewForwardDirWS, bool isPerspective,
                 const AAAAInstanceData instanceData, const AAAAMeshlet meshlet)
{
    const float3 coneApexWS = TransformObjectToWorld(meshlet.ConeApexCutoff.xyz, instanceData.ObjectToWorldMatrix);
    const float3 coneAxisWS = TransformObjectToWorldNormal(meshlet.ConeAxis.xyz, instanceData.WorldToObjectMatrix);
    const float3 viewDirWS = isPerspective ? normalize(coneApexWS - cameraPositionWS) : viewForwardDirWS;
    const float  dotResult = dot(viewDirWS, coneAxisWS);
    // using !>= handles the case when the meshlet's coneAxis is (0, 0, 0)
    // dotResult stores NaN in this case
    return !(dotResult >= meshlet.ConeApexCutoff.w);
}

#endif // AAAA_MESHLET_CULLING_INCLUDED