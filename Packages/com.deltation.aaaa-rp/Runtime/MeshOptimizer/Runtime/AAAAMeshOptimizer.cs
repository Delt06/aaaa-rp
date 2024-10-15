using System;
using System.Runtime.InteropServices;
using DELTation.AAAARP.Core;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using static DELTation.AAAARP.MeshOptimizer.Runtime.MeshOptimizerBindings;

namespace DELTation.AAAARP.MeshOptimizer.Runtime
{
    public static class AAAAMeshOptimizer
    {

        public enum SimplifyMode
        {
            Normal,
            Sloppy,
        }

        public static unsafe NativeArray<uint> OptimizeVertexCache(Allocator allocator, NativeArray<uint> indices, uint vertexCount)
        {
            var result = new NativeArray<uint>(indices.Length, allocator);
            meshopt_optimizeVertexCache((uint*) result.GetUnsafePtr(), (uint*) indices.GetUnsafeReadOnlyPtr(), (nuint) indices.Length, vertexCount);
            return result;
        }

        public static unsafe uint OptimizeIndexingInPlace(uint vertexCount, NativeArray<uint> indices, NativeArray<meshopt_Stream> streams)
        {
            var remap = new NativeArray<uint>((int) vertexCount, Allocator.Temp);
            nuint uniqueVertices = meshopt_generateVertexRemapMulti
            ((uint*) remap.GetUnsafePtr(),
                (uint*) indices.GetUnsafeReadOnlyPtr(), (nuint) indices.Length, vertexCount,
                (meshopt_Stream*) streams.GetUnsafeReadOnlyPtr(), (nuint) streams.Length
            );

            Assert.IsTrue(uniqueVertices <= vertexCount);

            meshopt_remapIndexBuffer((uint*) indices.GetUnsafePtr(), (uint*) indices.GetUnsafePtr(), (nuint) indices.Length, (uint*) remap.GetUnsafePtr());

            for (int index = 0; index < streams.Length; index++)
            {
                ref meshopt_Stream stream = ref streams.ElementAtRef(index);

                meshopt_remapVertexBuffer(stream.data, stream.data, vertexCount, stream.stride, (uint*) remap.GetUnsafePtr());
            }

            return (uint) uniqueVertices;
        }

        public static unsafe MeshletBuildResults BuildMeshlets(Allocator allocator, NativeArray<float> vertices, uint vertexPositionOffset,
            uint vertexPositionStride,
            NativeArray<uint> indices,
            in MeshletGenerationParams meshletGenerationParams)
        {
            using var _ = new ProfilingScope(Profiling.BuildMeshletsSampler);

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

        [MustUseReturnValue]
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

        public static unsafe MeshletBuildResults SimplifyMeshlets(Allocator allocator,
            NativeArray<MeshletBuildResults> meshletGroups,
            NativeArray<float> vertices, uint vertexPositionOffset, uint vertexPositionsStride,
            in MeshletGenerationParams meshletGenerationParams, SimplifyMode simplifyMode, float targetError, out float resultError)
        {
            using var _ = new ProfilingScope(Profiling.SimplifyMeshletsSampler);

            var localVertices = new NativeList<ClusterVertex>(Allocator.Temp);
            var localIndices = new NativeList<uint>(Allocator.Temp);

            byte* pVertexPositionsBytes = (byte*) vertices.GetUnsafeReadOnlyPtr() + vertexPositionOffset;

            using (new ProfilingScope(Profiling.SimplifyMeshletsSharedVerticesSampler))
            {
                foreach (MeshletBuildResults group in meshletGroups)
                {
                    foreach (meshopt_Meshlet meshlet in group.Meshlets)
                    {
                        int localOffset = localVertices.Length;

                        for (uint v = 0; v < meshlet.VertexCount; v++)
                        {
                            uint globalIndex = group.Vertices[(int) (meshlet.VertexOffset + v)];
                            localVertices.Add(new ClusterVertex
                                {
                                    Position = *(float3*) (pVertexPositionsBytes + globalIndex * vertexPositionsStride),
                                    Index = globalIndex,
                                }
                            );
                        }

                        for (uint t = 0; t < meshlet.TriangleCount; t++)
                        {
                            localIndices.Add((uint) (localOffset + group.Indices[(int) (meshlet.TriangleOffset + t * 3 + 0)]));
                            localIndices.Add((uint) (localOffset + group.Indices[(int) (meshlet.TriangleOffset + t * 3 + 1)]));
                            localIndices.Add((uint) (localOffset + group.Indices[(int) (meshlet.TriangleOffset + t * 3 + 2)]));
                        }
                    }
                }

                var meshoptStreams = new NativeArray<meshopt_Stream>(1, Allocator.Temp);
                meshoptStreams[0] = new meshopt_Stream
                {
                    data = localVertices.GetUnsafePtr(),
                    size = (nuint) UnsafeUtility.SizeOf<ClusterVertex>(),
                    stride = (nuint) UnsafeUtility.SizeOf<ClusterVertex>(),
                };

                uint newVertexCount = OptimizeIndexingInPlace((uint) localVertices.Length, localIndices.AsArray(), meshoptStreams);
                localVertices.Length = (int) newVertexCount;
                meshoptStreams.Dispose();
            }

            // ReSharper disable once PossibleLossOfFraction
            int targetIndexCount = (int) (localIndices.Length / 3 * 0.5f * 3);
            float resultErrorValue = 0.0f;

            int simplifiedIndexCount = simplifyMode switch
            {
                SimplifyMode.Normal => (int) meshopt_simplify(localIndices.GetUnsafePtr(), localIndices.GetUnsafePtr(), (nuint) localIndices.Length,
                    (float*) localVertices.GetUnsafePtr(), (nuint) localVertices.Length, (nuint) UnsafeUtility.SizeOf<ClusterVertex>(),
                    (nuint) targetIndexCount, targetError, (uint) meshopt_SimplifyOptions.LockBorder, &resultErrorValue
                ),
                SimplifyMode.Sloppy => (int) meshopt_simplifySloppy(localIndices.GetUnsafePtr(), localIndices.GetUnsafePtr(), (nuint) localIndices.Length,
                    (float*) localVertices.GetUnsafePtr(), (nuint) localVertices.Length, (nuint) UnsafeUtility.SizeOf<ClusterVertex>(),
                    (nuint) targetIndexCount, targetError, &resultErrorValue
                ),
                var _ => throw new ArgumentOutOfRangeException(nameof(simplifyMode), simplifyMode, null),
            };

            localIndices.Length = simplifiedIndexCount;

            var globalIndices = new NativeList<uint>(localIndices.Length, Allocator.Temp);

            foreach (uint localIndex in localIndices)
            {
                globalIndices.Add(localVertices[(int) localIndex].Index);
            }

            resultError = resultErrorValue;
            return BuildMeshlets(allocator, vertices, vertexPositionOffset, vertexPositionsStride, globalIndices.AsArray(), meshletGenerationParams);
        }

        public static unsafe void SpatialSortTrianglesInPlace(NativeArray<uint> indices, NativeArray<float> vertices, uint vertexCount,
            uint vertexPositionOffset,
            uint vertexPositionsStride)
        {
            uint* pIndices = (uint*) indices.GetUnsafePtr();
            float* pVertexPositions = (float*) ((byte*) vertices.GetUnsafeReadOnlyPtr() + vertexPositionOffset);
            meshopt_spatialSortTriangles(pIndices, pIndices, (nuint) indices.Length, pVertexPositions, vertexCount, vertexPositionsStride);
        }

        public static unsafe NativeArray<T> SpatialSort<T>(NativeArray<T> items, NativeArray<float3> sortPositions, Allocator allocator) where T : struct
        {
            Assert.IsTrue(items.Length == sortPositions.Length);

            int itemsCount = items.Length;

            var remap = new NativeArray<uint>(itemsCount, Allocator.Temp);
            meshopt_spatialSortRemap((uint*) remap.GetUnsafePtr(), (float*) sortPositions.GetUnsafePtr(), (nuint) itemsCount,
                (nuint) UnsafeUtility.SizeOf<float3>()
            );

            var sortedItems = new NativeArray<T>(itemsCount, allocator);

            for (int i = 0; i < itemsCount; i++)
            {
                uint remapIndex = remap[i];
                if (remapIndex != ~0u)
                {
                    sortedItems.ElementAtRef((int) remapIndex) = items[i];
                }
            }

            return sortedItems;
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

            public void Dispose(JobHandle jobHandle)
            {
                Meshlets.Dispose(jobHandle);
                Vertices.Dispose(jobHandle);
                Indices.Dispose(jobHandle);
            }

            public void Dispose()
            {
                Dispose(default);
            }
        }

        public struct MeshletGenerationParams
        {
            public uint MaxVertices;
            public uint MaxTriangles;
            public float ConeWeight;
        }

        private static class Profiling
        {
            public static readonly ProfilingSampler BuildMeshletsSampler = new(nameof(BuildMeshlets));
            public static readonly ProfilingSampler SimplifyMeshletsSampler = new(nameof(SimplifyMeshlets));
            public static readonly ProfilingSampler SimplifyMeshletsSharedVerticesSampler = new(nameof(SimplifyMeshlets) + "_SharedVertices");
        }
    }
}