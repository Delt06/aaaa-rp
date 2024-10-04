using Unity.Mathematics;
using UnityEngine;

namespace DELTation.AAAARP.Lighting
{
    public static class AAAALightingUtils
    {
        public static float3 ExtractDirection(Matrix4x4 localToWorldMatrix) =>
            -((float4) localToWorldMatrix.GetColumn(2)).xyz;
    }
}