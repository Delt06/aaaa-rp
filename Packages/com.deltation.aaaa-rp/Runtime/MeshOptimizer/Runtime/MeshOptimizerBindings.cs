using System;
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

        [DllImport(MeshOptimizerDLL, CharSet = CharSet, CallingConvention = CallingConvention)]
        public static extern nuint meshopt_simplify(uint* destination, uint* indices, nuint indexCount, float* vertexPositions, nuint vertexCount,
            nuint vertexPositionsStride, nuint targetIndexCount, float targetError, uint options, float* resultError = null);

        [DllImport(MeshOptimizerDLL, CharSet = CharSet, CallingConvention = CallingConvention)]
        public static extern void meshopt_optimizeVertexCache(uint* destination, uint* indices, nuint indexCount, nuint vertexCount);

        [DllImport(MeshOptimizerDLL, CharSet = CharSet, CallingConvention = CallingConvention)]
        public static extern nuint meshopt_generateVertexRemapMulti(uint* destination, uint* indices, nuint indexCount, nuint vertexCount,
            meshopt_Stream* streams, nuint streamCount);

        [DllImport(MeshOptimizerDLL, CharSet = CharSet, CallingConvention = CallingConvention)]
        public static extern void meshopt_remapVertexBuffer(void* destination, void* vertices, nuint vertexCount, nuint vertexSize, uint* remap);

        [DllImport(MeshOptimizerDLL, CharSet = CharSet, CallingConvention = CallingConvention)]
        public static extern void meshopt_remapIndexBuffer(uint* destination, uint* indices, nuint indexCount, uint* remap);
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

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [Flags]
    public enum meshopt_SimplifyOptions : uint
    {
        None = 0,
        /* Do not move vertices that are located on the topological border (vertices on triangle edges that don't have a paired triangle). Useful for simplifying portions of the larger mesh. */
        LockBorder = 1 << 0,
        /* Improve simplification performance assuming input indices are a sparse subset of the mesh. Note that error becomes relative to subset extents. */
        Sparse = 1 << 1,
        /* Treat error limit and resulting error as absolute instead of relative to mesh extents. */
        ErrorAbsolute = 1 << 2,
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public unsafe struct meshopt_Stream
    {
        public void* data;
        public nuint size;
        public nuint stride;
    }
}