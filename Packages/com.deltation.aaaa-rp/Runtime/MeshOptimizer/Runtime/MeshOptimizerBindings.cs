using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DELTation.AAAARP.MeshOptimizer.Runtime
{
    internal static unsafe class MeshOptimizerBindings
    {
        private const string MeshOptimizerDLL = "meshoptimizer.dll";
        private const CharSet CharSet = System.Runtime.InteropServices.CharSet.Auto;
        private const CallingConvention CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl;
        
        [DllImport(MeshOptimizerDLL, CharSet = CharSet, CallingConvention = CallingConvention)]
        public static extern nuint meshopt_buildMeshlets(meshopt_Meshlet* meshlets, uint* meshletVertices, byte* meshletTriangles, uint* indices,
            nuint indexCount, float* vertexPositions, nuint vertexCount, nuint vertexPositionsStride, nuint maxVertices, nuint maxTriangles, float coneWeight);
        
        [DllImport(MeshOptimizerDLL, CharSet = CharSet, CallingConvention = CallingConvention)]
        public static extern nuint meshopt_buildMeshletsBound(nuint indexCount, nuint maxVertices, nuint maxTriangles);
        
        [DllImport(MeshOptimizerDLL, CharSet = CharSet, CallingConvention = CallingConvention)]
        public static extern meshopt_Bounds meshopt_computeMeshletBounds(uint* meshletVertices, byte* meshletTriangles, nuint triangleCount,
            float* vertexPositions, nuint vertexCount, nuint vertexPositionsStride);
    }
    
    [StructLayout(LayoutKind.Sequential)]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public struct meshopt_Meshlet
    {
        public uint VertexOffset;
        public uint TriangleOffset;
        
        /* number of vertices and triangles used in the meshlet; data is stored in consecutive range defined by offset and count */
        public uint VertexCount;
        public uint TriangleCount;
    }
    
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public unsafe struct meshopt_Bounds
    {
        /* bounding sphere, useful for frustum and occlusion culling */
        public fixed float Center[3];
        public float Radius;
        
        /* normal cone, useful for backface culling */
        public fixed float ConeApex[3];
        public fixed float ConeAxis[3];
        public float ConeCutoff; /* = cos(angle/2) */
        
        /* normal cone axis and cutoff, stored in 8-bit SNORM format; decode using x/127.0 */
        public fixed sbyte coneAxisS8[3];
        public sbyte ConeCutoffS8;
    }
    
    [StructLayout(LayoutKind.Sequential, Pack = sizeof(byte))]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public struct sbyte3
    {
        public sbyte x;
        public sbyte y;
        public sbyte z;
    }
}