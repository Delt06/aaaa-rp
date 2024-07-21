using System;
using DELTation.AAAARP.MeshOptimizer.Runtime;
using DELTation.AAAARP.METIS.Runtime;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DELTation.AAAARP.Editor.Meshlets
{
    internal partial class AAAAMeshletCollectionAssetImporter
    {
        private static NativeArray<int> GroupMeshlets(MeshLODNodeLevel meshLODNodeLevel, int meshletsPerGroup, Allocator allocator)
        {
            int graphNodeCount = meshLODNodeLevel.Nodes.Length;
            int partitionsCount = Mathf.CeilToInt((float) graphNodeCount / (meshletsPerGroup - 1));
            if (partitionsCount <= 1)
            {
                var allNodes = new NativeArray<int>(meshLODNodeLevel.Nodes.Length, allocator, NativeArrayOptions.UninitializedMemory);

                for (int i = 0; i < allNodes.Length; i++)
                {
                    allNodes[i] = i;
                }

                return allNodes;
            }


            NativeArray<NativeHashSet<Edge>> edgeSets = CollectEdgeSets(meshLODNodeLevel, Allocator.TempJob);

            NativeArray<int> adjacencyMatrix = CreateAdjacencyMatrix(edgeSets, Allocator.TempJob);
            edgeSets.Dispose();


            var adjacencyIndexList = new NativeArray<int>(graphNodeCount + 1, Allocator.Temp)
            {
                [0] = 0,
            };
            var adjacencyList = new NativeList<int>(graphNodeCount, Allocator.Temp);
            var adjacencyWeightList = new NativeList<int>(graphNodeCount, Allocator.Temp);

            for (int node1 = 0; node1 < graphNodeCount; node1++)
            {
                int totalEdgeCount = 0;

                for (int node2 = 0; node2 < graphNodeCount; node2++)
                {
                    int weight = adjacencyMatrix[node1 * graphNodeCount + node2];
                    if (weight > 0)
                    {
                        adjacencyList.Add(node2);
                        adjacencyWeightList.Add(weight);
                        ++totalEdgeCount;
                    }
                }

                adjacencyIndexList[node1 + 1] = adjacencyIndexList[node1] + totalEdgeCount;
            }

            adjacencyMatrix.Dispose();

            var graphAdjacencyStructure = new AAAAMETIS.GraphAdjacencyStructure
            {
                VertexCount = graphNodeCount,
                AdjacencyIndexList = adjacencyIndexList,
                AdjacencyList = adjacencyList.AsArray(),
                AdjacencyWeightList = adjacencyWeightList.AsArray(),
            };

            NativeArray<METISOptions> options = AAAAMETIS.CreateOptions(Allocator.Temp);

            METISStatus status = AAAAMETIS.PartGraphKway(graphAdjacencyStructure, Allocator.Temp, partitionsCount, options,
                out NativeArray<int> vertexPartitioning
            );
            Assert.IsTrue(status == METISStatus.METIS_OK);

            adjacencyIndexList.Dispose();
            adjacencyList.Dispose();
            adjacencyWeightList.Dispose();
            options.Dispose();

            NativeArray<int> meshletGrouping =
                ConstructMeshletGroupingFromVertexPartitioning(vertexPartitioning, allocator, partitionsCount, meshletsPerGroup);

            vertexPartitioning.Dispose();

            return meshletGrouping;
        }

        private static unsafe NativeArray<int> ConstructMeshletGroupingFromVertexPartitioning(NativeArray<int> meshletPartitioning, Allocator allocator,
            int partitionsCount, int meshletsPerGroup)
        {
            var reversedGroups = new NativeArray<int>(partitionsCount * meshletsPerGroup, allocator,
                NativeArrayOptions.UninitializedMemory
            );
            UnsafeUtility.MemSet(reversedGroups.GetUnsafePtr(), (byte) 0xFFu, reversedGroups.Length * sizeof(int));

            for (int nodeIndex = 0; nodeIndex < meshletPartitioning.Length; nodeIndex++)
            {
                int groupIndex = meshletPartitioning[nodeIndex];

                bool foundPlace = false;

                for (int offsetInGroup = 0; offsetInGroup < meshletsPerGroup; offsetInGroup++)
                {
                    int itemIndex = groupIndex * meshletsPerGroup + offsetInGroup;
                    if (reversedGroups[itemIndex] < 0)
                    {
                        reversedGroups[itemIndex] = nodeIndex;
                        foundPlace = true;
                        break;
                    }
                }

                UnityEngine.Assertions.Assert.IsTrue(foundPlace);
            }

            return reversedGroups;
        }

        private static NativeArray<NativeHashSet<Edge>> CollectEdgeSets(MeshLODNodeLevel nodeLevel, Allocator allocator)
        {
            var edgeSets = new NativeArray<NativeHashSet<Edge>>(nodeLevel.Nodes.Length, allocator);

            for (int i = 0; i < edgeSets.Length; i++)
            {
                edgeSets[i] = new NativeHashSet<Edge>((int) (AAAAMeshletConfiguration.MaxMeshletTriangles * 3), allocator);
            }

            new CollectMeshletEdgesJob
                {
                    EdgeSets = edgeSets,
                    NodeLevel = nodeLevel,
                }.Schedule(edgeSets.Length, 4)
                .Complete();

            return edgeSets;
        }

        private static NativeArray<int> CreateAdjacencyMatrix(NativeArray<NativeHashSet<Edge>> edgeSets, Allocator allocator)
        {
            int graphNodeCount = edgeSets.Length;
            var adjacencyMatrix = new NativeArray<int>(graphNodeCount * graphNodeCount, allocator);

            var nodePairs = new NativeList<int2>(graphNodeCount * graphNodeCount, Allocator.TempJob);

            for (int node1 = 0; node1 < graphNodeCount; node1++)
            {
                for (int node2 = node1 + 1; node2 < graphNodeCount; node2++)
                {
                    nodePairs.Add(new int2(node1, node2));
                }
            }

            new FillAdjacencyMatrixJob
                {
                    NodeCount = graphNodeCount,
                    AdjacencyMatrix = adjacencyMatrix,
                    EdgeSets = edgeSets,
                    NodePairs = nodePairs.AsArray(),
                }.Schedule(nodePairs.Length, 4)
                .Complete();

            nodePairs.Dispose();
            return adjacencyMatrix;
        }

        [BurstCompile]
        private struct FillAdjacencyMatrixJob : IJobParallelFor
        {
            public int NodeCount;

            [ReadOnly]
            public NativeArray<int2> NodePairs;
            [NativeDisableContainerSafetyRestriction]
            [ReadOnly]
            public NativeArray<NativeHashSet<Edge>> EdgeSets;

            [WriteOnly] [NativeDisableParallelForRestriction]
            public NativeArray<int> AdjacencyMatrix;

            public void Execute(int index)
            {
                int2 nodes = NodePairs[index];
                NativeHashSet<Edge> edgeSet1 = EdgeSets[nodes.x];
                NativeHashSet<Edge> edgeSet2 = EdgeSets[nodes.y];

                int sharedEdgeCount = 0;

                foreach (Edge edge1 in edgeSet1)
                {
                    if (edgeSet2.Contains(edge1))
                    {
                        ++sharedEdgeCount;
                    }
                }

                AdjacencyMatrix[nodes.x * NodeCount + nodes.y] = sharedEdgeCount;
                AdjacencyMatrix[nodes.y * NodeCount + nodes.x] = sharedEdgeCount;
            }
        }

        [BurstCompile]
        private struct CollectMeshletEdgesJob : IJobParallelFor
        {
            [NativeDisableContainerSafetyRestriction]
            public MeshLODNodeLevel NodeLevel;
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<NativeHashSet<Edge>> EdgeSets;

            public void Execute(int index)
            {
                MeshLODNode lodNode = NodeLevel.Nodes[index];
                MeshLODNodeLevel.MeshletNodeList meshletsNodeList = NodeLevel.MeshletsNodeLists[lodNode.MeshletNodeListIndex];

                AAAAMeshOptimizer.MeshletBuildResults meshletBuildResults = meshletsNodeList.MeshletBuildResults;
                meshopt_Meshlet meshlet = meshletBuildResults.Meshlets[lodNode.MeshletIndex];

                NativeHashSet<Edge> edgeSet = EdgeSets[index];

                for (int i = 0; i < meshlet.TriangleCount; i++)
                {
                    int baseIndex = (int) (meshlet.TriangleOffset + i * 3);
                    uint index0 = meshletBuildResults.Vertices[(int) meshlet.VertexOffset + meshletBuildResults.Indices[baseIndex + 0]];
                    uint index1 = meshletBuildResults.Vertices[(int) meshlet.VertexOffset + meshletBuildResults.Indices[baseIndex + 1]];
                    uint index2 = meshletBuildResults.Vertices[(int) meshlet.VertexOffset + meshletBuildResults.Indices[baseIndex + 2]];

                    edgeSet.Add(new Edge(index0, index1));
                    edgeSet.Add(new Edge(index1, index2));
                    edgeSet.Add(new Edge(index2, index0));
                }
            }
        }

        private readonly struct Edge : IEquatable<Edge>
        {
            public readonly uint Index0;
            public readonly uint Index1;

            public Edge(uint index0, uint index1)
            {
                Index0 = math.min(index0, index1);
                Index1 = math.max(index0, index1);
            }

            public bool Equals(Edge other) =>
                Index0 == other.Index0 && Index1 == other.Index1;

            public override bool Equals(object obj) => obj is Edge other && Equals(other);

            public override int GetHashCode() => unchecked((int) (Index0 * 397 ^ Index1));
        }
    }
}