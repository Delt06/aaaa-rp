using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace DELTation.AAAARP.Core
{
    public static class AAAAMathUtils
    {
        public static int AlignUp(int value, int alignment)
        {
            if (alignment == 0)
            {
                return value;
            }
            return value + alignment - 1 & -alignment;
        }

        public static int2 AlignUp(int2 value, int2 alignment) => select(value, value + alignment - 1 & -alignment, alignment != 0);
        public static int3 AlignUp(int3 value, int3 alignment) => select(value, value + alignment - 1 & -alignment, alignment != 0);

        public static float3x3 Inverse3X3(float3x3 m)
        {
            float3 row0 = m[0];
            float3 row1 = m[1];
            float3 row2 = m[2];

            float3 col0 = cross(row1, row2);
            float3 col1 = cross(row2, row0);
            float3 col2 = cross(row0, row1);

            float det = dot(row0, col0);

            return transpose(float3x3(col0, col1, col2) / det);
        }

        public static float4x4 AffineInverse3D(float4x4 m)
        {
            var r = (float3x3) m;
            float3 t = m.c3.xyz;

            float3x3 invR = Inverse3X3(r);
            float3 invT = -mul(invR, t);

            return float4x4(
                invR.c0.x, invR.c1.x, invR.c2.x, invT.x,
                invR.c0.y, invR.c1.y, invR.c2.y, invT.y,
                invR.c0.z, invR.c1.z, invR.c2.z, invT.z,
                0.0f, 0.0f, 0.0f, 1.0f
            );
        }
    }
}