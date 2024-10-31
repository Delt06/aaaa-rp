using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Passes
{
    [GenerateHLSL(PackingRules.Exact, needAccessors = false)]
    [StructLayout(LayoutKind.Auto)]
    public unsafe struct GPUCullingContext
    {
        public const int MaxCullingContextsPerBatch = 8;

        public float4x4 ViewProjectionMatrix;
        public float4x4 ViewMatrix;
        public float4 CameraPosition;

        [HLSLArray(6, typeof(Vector4))]
        public fixed float FrustumPlanes[6 * 4];
        public float4 CullingSphereLS;

        public int PassMask;
        public int CameraIsPerspective;
        public uint BaseStartInstance;
        public uint MeshletListBuildJobsOffset;
        public uint MeshletRenderRequestsOffset;

        public uint Padding0;
        public uint Padding1;
        public uint Padding2;
    }

    [GenerateHLSL(PackingRules.Exact, needAccessors = false)]
    [StructLayout(LayoutKind.Auto)]
    public struct GPULODSelectionContext
    {
        public float4x4 ViewProjectionMatrix;
        public float4 CameraPosition;
        public float4 CameraUp;
        public float4 CameraRight;
        public float2 ScreenSizePixels;

        public uint Padding0;
        public uint Padding1;
    }
}