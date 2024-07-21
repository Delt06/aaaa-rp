using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;

namespace DELTation.AAAARP.METIS.Runtime
{
    public class TestMetis : MonoBehaviour
    {
        private void Awake()
        {
            var graphAdjacencyStructure = new AAAAMETIS.GraphAdjacencyStructure
            {
                VertexCount = 6,
                AdjacencyIndexList = new NativeList<int>(Allocator.Temp) { 0, 2, 5, 7, 10, 13, 14 }.AsArray(),
                AdjacencyList = new NativeList<int>(Allocator.Temp) { 1, 4, 0, 2, 4, 1, 3, 2, 4, 5, 0, 1, 3, 3 }.AsArray(),
                AdjacencyWeightList = new NativeList<int>(Allocator.Temp) { 1, 1, 1, 1, 1, 1, 1, 1, 10, 1, 1, 1, 10, 1 }.AsArray(),
            };
            const int numPartitions = 2;

            NativeArray<METISOptions> options = AAAAMETIS.CreateOptions(Allocator.Temp);
            METISStatus status = AAAAMETIS.PartGraphKway(graphAdjacencyStructure, Allocator.Temp, numPartitions, options,
                out NativeArray<int> vertexPartitioning
            );
            Assert.IsTrue(status == METISStatus.METIS_OK);

            for (int i = 0; i < vertexPartitioning.Length; i++)
            {
                Debug.Log($"[{i}] = {vertexPartitioning[i]}");
            }
        }
    }
}