using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Passes
{
    [GenerateHLSL(PackingRules.Exact, needAccessors = false)]
    public unsafe struct GPUCullingContext
    {
        public const int MaxCullingContextsPerBatch = 8;

        public float4x4 CullingViewProjection;
        public float4x4 CullingView;
        public float4 CullingCameraPosition;

        [HLSLArray(6, typeof(Vector4))]
        public fixed float CullingFrustumPlanes[6 * 4];
        public float4 CullingSphereLS;

        public int CullingPassMask;
        public int CullingCameraIsPerspective;
    }

    [GenerateHLSL(PackingRules.Exact, needAccessors = false)]
    public struct GPULODSelectionContext
    {
        public float4x4 LODCameraViewProjection;
        public float4 LODCameraPosition;
        public float4 LODCameraUp;
        public float4 LODCameraRight;
        public float2 LODScreenSizePixels;
    }
}