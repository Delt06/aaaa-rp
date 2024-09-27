using System;
using Unity.Mathematics;

namespace DELTation.AAAARP.Utils
{
    internal static class CubemapUtils
    {
        public const int SideCount = 6;

        private static readonly SideOrientation[] SideOrientations =
        {
            // +X 
            new() { Forward = math.float4(1, 0, 0, 0), Up = math.float4(0, 1, 0, 0) },

            // -X
            new() { Forward = math.float4(-1, 0, 0, 0), Up = math.float4(0, 1, 0, 0) },

            // +Y
            new() { Forward = math.float4(0, 1, 0, 0), Up = math.float4(0, 0, -1, 0) },

            // -Y
            new() { Forward = math.float4(0, -1, 0, 0), Up = math.float4(0, 0, 1, 0) },

            // +X 
            new() { Forward = math.float4(0, 0, 1, 0), Up = math.float4(0, 1, 0, 0) },

            // -X
            new() { Forward = math.float4(0, 0, -1, 0), Up = math.float4(0, 1, 0, 0) },
        };

        public static ReadOnlySpan<SideOrientation> GetSideOrientations() => SideOrientations;

        public struct SideOrientation
        {
            public float4 Forward;
            public float4 Up;
        }
    }
}