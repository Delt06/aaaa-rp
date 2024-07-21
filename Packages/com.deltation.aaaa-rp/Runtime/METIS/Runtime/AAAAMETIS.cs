using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.METIS.Runtime
{
    public static class AAAAMETIS
    {
        public static unsafe NativeArray<METISOptions> CreateOptions(Allocator allocator)
        {
            var options = new NativeArray<METISOptions>(METISBindings.METIS_NOPTIONS, allocator, NativeArrayOptions.UninitializedMemory);
            UnsafeUtility.MemSet(options.GetUnsafePtr(), (byte) 0xFFu, options.Length * sizeof(METISOptions));
            return options;
        }

        public static unsafe METISStatus PartGraphKway(GraphAdjacencyStructure graphAdjacencyStructure, Allocator allocator, int partitions,
            NativeArray<METISOptions> options,
            out NativeArray<int> vertexPartitioning)
        {
            using var _ = new ProfilingScope(Profiling.PartGraphKwaySampler);

            Assert.IsTrue(partitions > 1);
            graphAdjacencyStructure.Validate();

            int numConstraints = 1;
            int edgeCut = 0;

            int* xadj = (int*) graphAdjacencyStructure.AdjacencyIndexList.GetUnsafePtr();
            int* adjncy = (int*) graphAdjacencyStructure.AdjacencyList.GetUnsafePtr();
            int* adjwgt = (int*) (graphAdjacencyStructure.AdjacencyWeightList.IsCreated
                ? graphAdjacencyStructure.AdjacencyWeightList.GetUnsafePtr()
                : null);
            vertexPartitioning = new NativeArray<int>(graphAdjacencyStructure.VertexCount, allocator, NativeArrayOptions.UninitializedMemory);

            METISStatus status = METISBindings.METIS_PartGraphKway(&graphAdjacencyStructure.VertexCount, &numConstraints,
                xadj, adjncy,
                null, null, adjwgt,
                &partitions, null,
                null, (METISOptions*) options.GetUnsafePtr(), &edgeCut, (int*) vertexPartitioning.GetUnsafePtr()
            );
            if (status != METISStatus.METIS_OK)
            {
                vertexPartitioning.Dispose();
                vertexPartitioning = default;
            }
            return status;
        }

        private static class Profiling
        {
            public static readonly ProfilingSampler PartGraphKwaySampler = new(nameof(PartGraphKway));
        }

        public struct GraphAdjacencyStructure
        {
            public int VertexCount;
            public NativeArray<int> AdjacencyIndexList;
            public NativeArray<int> AdjacencyList;
            public NativeArray<int> AdjacencyWeightList;

            public void Validate()
            {
                Assert.IsTrue(VertexCount >= 1);

                Assert.IsTrue(AdjacencyIndexList.IsCreated);
                Assert.IsTrue(AdjacencyIndexList.Length == VertexCount + 1);
                foreach (int adjacencyIndex in AdjacencyIndexList)
                {
                    Assert.IsTrue(adjacencyIndex <= AdjacencyList.Length);
                }

                Assert.IsTrue(AdjacencyList.IsCreated);
                foreach (int index in AdjacencyList)
                {
                    Assert.IsTrue(index < VertexCount);
                }

                if (AdjacencyWeightList.IsCreated)
                {
                    Assert.IsTrue(AdjacencyList.Length == AdjacencyWeightList.Length);
                }
            }
        }
    }
}