using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace DELTation.AAAARP.METIS.Runtime
{
    public class TestMetis : MonoBehaviour
    {
        private unsafe void Awake()
        {
            int numVerts = 6;
            int numConstraints = 1;
            var adjncy = new NativeList<int>(Allocator.Temp) { 1, 4, 0, 2, 4, 1, 3, 2, 4, 5, 0, 1, 3, 3 };
            var adjwgt = new NativeList<int>(Allocator.Temp) { 1, 1, 1, 1, 1, 1, 1, 1, 10, 1, 1, 1, 10, 1 };
            var xadj = new NativeList<int>(Allocator.Temp) { 0, 2, 5, 7, 10, 13, 14 };
            int numPartitions = 2;

            METISBindings.METISOptions* options = stackalloc METISBindings.METISOptions[METISBindings.METIS_NOPTIONS];
            UnsafeUtility.MemSet(options, (byte) 0xFFu, METISBindings.METIS_NOPTIONS * sizeof(METISBindings.METISOptions));

            int edgeCut = 0;

            var part = new NativeArray<int>(numVerts, Allocator.Temp);

            METISBindings.METIS_PartGraphKway(&numVerts, &numConstraints, xadj.GetUnsafePtr(), adjncy.GetUnsafePtr(), null, null, adjwgt.GetUnsafePtr(),
                &numPartitions, null,
                null, options, &edgeCut, (int*) part.GetUnsafePtr()
            );
        }
    }
}