using System;
using System.Runtime.InteropServices;
using DELTation.AAAARP.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Assertions;
using static DELTation.AAAARP.MeshOptimizer.Runtime.MeshOptimizerBindings;

namespace DELTation.AAAARP.MeshOptimizer.Runtime
{
    public static class AAAAMeshOptimizer
    {
        public static unsafe MeshletBuildResults BuildMeshlets(Allocator allocator, NativeArray<float> vertices, uint vertexPositionOffset,
            uint vertexPositionStride,
            NativeArray<uint> indices,
            in MeshletGenerationParams meshletGenerationParams)
        {
            Assert.IsTrue(vertices.Length > 0);
            Assert.IsTrue(indices.Length > 0);
            Assert.IsTrue(vertexPositionStride > 0);

            Assert.IsTrue(meshletGenerationParams.MaxVertices > 0);
            Assert.IsTrue(meshletGenerationParams.MaxTriangles > 0);
            nuint maxMeshlets = meshopt_buildMeshletsBound((nuint) indices.Length, meshletGenerationParams.MaxVertices, meshletGenerationParams.MaxTriangles);
            Assert.IsTrue(maxMeshlets > 0);

            var meshlets = new NativeArray<meshopt_Meshlet>((int) maxMeshlets, allocator);
            var meshletVertices = new NativeArray<uint>((int) (maxMeshlets * meshletGenerationParams.MaxVertices), allocator);
            var meshletIndices = new NativeArray<byte>((int) (maxMeshlets * meshletGenerationParams.MaxTriangles * 3), allocator);

            uint floatsInVertex = vertexPositionStride / sizeof(float);
            nuint meshletCount = meshopt_buildMeshlets(
                (meshopt_Meshlet*) meshlets.GetUnsafePtr(), (uint*) meshletVertices.GetUnsafePtr(), (byte*) meshletIndices.GetUnsafePtr(),
                (uint*) indices.GetUnsafeReadOnlyPtr(), (nuint) indices.Length,
                (float*) ((byte*) vertices.GetUnsafeReadOnlyPtr() + vertexPositionOffset), (nuint) vertices.Length / floatsInVertex, vertexPositionStride,
                meshletGenerationParams.MaxVertices, meshletGenerationParams.MaxTriangles, meshletGenerationParams.ConeWeight
            );

            ref readonly meshopt_Meshlet lastMeshlet = ref meshlets.ElementAtRefReadonly((int) (meshletCount - 1u));
            return new MeshletBuildResults
            {
                Meshlets = meshlets.GetSubArray(0, (int) meshletCount),
                Vertices = meshletVertices.GetSubArray(0, (int) (lastMeshlet.VertexOffset + lastMeshlet.VertexCount)),
                Indices = meshletIndices.GetSubArray(0, (int) (lastMeshlet.TriangleOffset + (lastMeshlet.TriangleCount * 3 + 3 & ~3))),
            };
        }

        public static unsafe meshopt_Bounds ComputeMeshletBounds(in MeshletBuildResults buildResults, int meshletIndex,
            NativeArray<float> vertices, uint vertexPositionOffset,
            uint vertexPositionStride)
        {
            ref readonly meshopt_Meshlet meshlet = ref buildResults.Meshlets.ElementAtRefReadonly(meshletIndex);

            uint floatsInVertex = vertexPositionStride / sizeof(float);
            return meshopt_computeMeshletBounds(
                buildResults.Vertices.ElementPtr((int) meshlet.VertexOffset),
                buildResults.Indices.ElementPtr((int) meshlet.TriangleOffset),
                meshlet.TriangleCount,
                (float*) ((byte*) vertices.GetUnsafeReadOnlyPtr() + vertexPositionOffset), (nuint) vertices.Length / floatsInVertex, vertexPositionStride
            );
        }

        public static unsafe MeshletBuildResults SimplifyMeshletCluster(Allocator allocator,
            MeshletBuildResults meshletCluster,
            NativeArray<float> vertices, uint vertexPositionOffset, uint vertexPositionsStride,
            in MeshletGenerationParams meshletGenerationParams
        )
        {
            var localVertices = new NativeList<ClusterVertex>(Allocator.Temp);
            var localIndices = new NativeList<uint>(Allocator.Temp);

            byte* pVertexPositionsBytes = (byte*) vertices.GetUnsafeReadOnlyPtr() + vertexPositionOffset;

            foreach (meshopt_Meshlet meshlet in meshletCluster.Meshlets)
            {
                int localOffset = localVertices.Length;

                for (uint v = 0; v < meshlet.VertexCount; v++)
                {
                    uint globalIndex = meshletCluster.Vertices[(int) (meshlet.VertexOffset + v)];
                    localVertices.Add(new ClusterVertex
                        {
                            Position = *(float3*) (pVertexPositionsBytes + globalIndex * vertexPositionsStride),
                            Index = globalIndex,
                        }
                    );
                }

                for (uint t = 0; t < meshlet.TriangleCount; t++)
                {
                    localIndices.Add((uint) (localOffset + meshletCluster.Indices[(int) (meshlet.TriangleOffset + t * 3 + 0)]));
                    localIndices.Add((uint) (localOffset + meshletCluster.Indices[(int) (meshlet.TriangleOffset + t * 3 + 1)]));
                    localIndices.Add((uint) (localOffset + meshletCluster.Indices[(int) (meshlet.TriangleOffset + t * 3 + 2)]));
                }
            }

            // ReSharper disable once PossibleLossOfFraction
            int targetIndexCount = (int) (localIndices.Length / 3 * 0.5 * 3);
            int simplifiedIndexCount = (int) meshopt_simplify(localIndices.GetUnsafePtr(), localIndices.GetUnsafePtr(), (nuint) localIndices.Length,
                (float*) localVertices.GetUnsafePtr(), (nuint) localVertices.Length, (nuint) UnsafeUtility.SizeOf<ClusterVertex>(), (nuint) targetIndexCount,
                1e-2f, (uint) meshopt_SimplifyOptions.LockBorder
            );
            localIndices.Length = simplifiedIndexCount;

            var globalIndices = new NativeList<uint>(localIndices.Length, Allocator.Temp);

            foreach (uint localIndex in localIndices)
            {
                globalIndices.Add(localVertices[(int) localIndex].Index);
            }

            return BuildMeshlets(allocator, vertices, vertexPositionOffset, vertexPositionsStride, globalIndices.AsArray(), meshletGenerationParams);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ClusterVertex
        {
            public float3 Position;

            // Index in the original vertex buffer
            public uint Index;
        }

        public struct MeshletBuildResults : IDisposable
        {
            public NativeArray<meshopt_Meshlet> Meshlets;
            public NativeArray<uint> Vertices;
            public NativeArray<byte> Indices;

            public void Dispose()
            {
                Meshlets.Dispose();
                Vertices.Dispose();
                Indices.Dispose();
            }

            public MeshletBuildResults MeshletSubArray(int startIndex, int count) =>
                new()
                {
                    Meshlets = Meshlets.GetSubArray(startIndex, count),
                    Vertices = Vertices,
                    Indices = Indices,
                };
        }

        public struct MeshletGenerationParams
        {
            public uint MaxVertices;
            public uint MaxTriangles;
            public float ConeWeight;
        }
    }
}