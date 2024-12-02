using System;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.Meshlets;
using DELTation.AAAARP.MeshOptimizer.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Editor.Meshlets
{
    internal static partial class AAAAMeshletCollectionBuilder
    {
        public static unsafe void Generate(AAAAMeshletCollectionAsset meshletCollection, in Parameters parameters)
        {
            meshletCollection.SourceMeshGUID = parameters.SourceMeshGUID;
            meshletCollection.SourceMeshName = parameters.Mesh.name;
            meshletCollection.SourceSubmeshIndex = parameters.SubMeshIndex;
            meshletCollection.Bounds = parameters.Mesh.bounds;

            using Mesh.MeshDataArray dataArray = Mesh.AcquireReadOnlyMeshData(parameters.Mesh);
            Mesh.MeshData data = dataArray[0];
            uint vertexBufferStride = (uint) data.GetVertexBufferStride(0);
            uint vertexPositionOffset = (uint) data.GetVertexAttributeOffset(VertexAttribute.Position);
            NativeArray<float> vertexData = data.GetVertexData<float>();

            NativeArray<uint> indexDataU32;
            if (data.indexFormat == IndexFormat.UInt16)
            {
                NativeArray<ushort> indexDataU16 = data.GetIndexData<ushort>();
                indexDataU32 = CastIndices16To32(indexDataU16, Allocator.TempJob);
                indexDataU16.Dispose();
            }
            else
            {
                NativeArray<uint> indexData = data.GetIndexData<uint>();
                indexDataU32 = new NativeArray<uint>(indexData.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                indexDataU32.CopyFrom(indexData);
                indexData.Dispose();
            }

            SubMeshDescriptor subMeshDescriptor = data.GetSubMesh(parameters.SubMeshIndex);
            indexDataU32 = indexDataU32.GetSubArray(subMeshDescriptor.indexStart, subMeshDescriptor.indexCount);
            uint baseVertex = (uint) subMeshDescriptor.baseVertex;

            for (int i = 0; i < indexDataU32.Length; i++)
            {
                indexDataU32[i] += baseVertex;
            }

            int uvStream = data.GetVertexAttributeStream(VertexAttribute.TexCoord0);
            uint uvStreamStride = (uint) (uvStream >= 0 ? data.GetVertexBufferStride(uvStream) : 0);
            NativeArray<float> uvVertexData = uvStream >= 0 ? data.GetVertexData<float>(uvStream) : default;
            byte* pVerticesUV = uvVertexData.IsCreated ? (byte*) uvVertexData.GetUnsafeReadOnlyPtr() : null;
            uint vertexUVOffset = (uint) data.GetVertexAttributeOffset(VertexAttribute.TexCoord0);

            uint vertexCount = (uint) subMeshDescriptor.vertexCount;

            if (parameters.OptimizeVertexCache)
            {
                NativeArray<uint> sourceIndices = indexDataU32;
                indexDataU32 = AAAAMeshOptimizer.OptimizeVertexCache(Allocator.TempJob, sourceIndices, vertexCount);
                sourceIndices.Dispose();
            }

            AAAAMeshOptimizer.MeshletGenerationParams meshletGenerationParams = AAAAMeshletCollectionAsset.MeshletGenerationParams;
            const Allocator allocator = Allocator.TempJob;
            AAAAMeshOptimizer.MeshletBuildResults mainMeshletBuildResults = AAAAMeshOptimizer.BuildMeshlets(allocator,
                vertexData, vertexPositionOffset, vertexBufferStride, indexDataU32,
                meshletGenerationParams
            );

            var meshLODLevels = new NativeList<MeshLODNodeLevel>(allocator);
            var topLOD = new MeshLODNodeLevel
            {
                Nodes = new NativeArray<MeshLODNode>(mainMeshletBuildResults.Meshlets.Length, allocator),
                MeshletsNodeLists = new NativeArray<MeshLODNodeLevel.MeshletNodeList>(1, allocator)
                {
                    [0] = new MeshLODNodeLevel.MeshletNodeList
                    {
                        MeshletBuildResults = mainMeshletBuildResults,
                    },
                },
            };

            for (int i = 0; i < topLOD.Nodes.Length; ++i)
            {
                AAAAMeshOptimizer.MeshletBuildResults meshletBuildResults = topLOD.MeshletsNodeLists[0].MeshletBuildResults;
                meshopt_Meshlet meshlet = meshletBuildResults.Meshlets[i];
                meshopt_Bounds bounds =
                    AAAAMeshOptimizer.ComputeMeshletBounds(meshletBuildResults, i, vertexData, vertexPositionOffset, vertexBufferStride);

                topLOD.TriangleCount += meshlet.TriangleCount;
                topLOD.Nodes[i] = new MeshLODNode
                {
                    MeshletNodeListIndex = 0,
                    MeshletIndex = i,
                    ChildGroupIndex = -1,
                    Error = 0.0f,
                    Bounds = math.float4(bounds.Center[0], bounds.Center[1], bounds.Center[2], bounds.Radius),
                };
            }
            meshLODLevels.Add(topLOD);

            var vertexLayout = new AAAAMeshOptimizer.VertexLayout
            {
                Vertices = vertexData,
                UV = uvVertexData,
                PositionOffset = vertexPositionOffset,
                PositionStride = vertexBufferStride,
                UVOffset = vertexUVOffset,
                UVStride = vertexBufferStride,
            };
            BuildLodGraph(meshLODLevels, allocator, vertexLayout, meshletGenerationParams, parameters);

            int meshLODNodes = 0;
            int totalMeshlets = 0;
            int totalVertices = 0;
            int totalIndices = 0;

            foreach (MeshLODNodeLevel level in meshLODLevels)
            {
                foreach (NativeList<int> levelGroup in level.Groups)
                {
                    meshLODNodes += levelGroup.Length;
                    totalMeshlets += levelGroup.Length;

                    NativeArray<MeshLODNodeLevel.MeshletNodeList> meshletsNodeLists = level.MeshletsNodeLists;

                    foreach (int nodeIndex in levelGroup)
                    {
                        MeshLODNode node = level.Nodes[nodeIndex];
                        MeshLODNodeLevel.MeshletNodeList meshletNodeList = meshletsNodeLists[node.MeshletNodeListIndex];

                        AAAAMeshOptimizer.MeshletBuildResults meshletBuildResults = meshletNodeList.MeshletBuildResults;
                        meshopt_Meshlet meshlet = meshletBuildResults.Meshlets[node.MeshletIndex];
                        totalVertices += (int) meshlet.VertexCount;
                        totalIndices += (int) meshlet.TriangleCount * 3;
                    }
                }
            }

            if (meshLODNodes > AAAAMeshletComputeShaders.MaxMeshLODNodesPerInstance)
            {
                parameters.LogErrorHandler($"Mesh LOD node count exceeds the limit: {meshLODNodes}/{AAAAMeshletComputeShaders.MaxMeshLODNodesPerInstance}."
                );
            }

            meshletCollection.LeafMeshletCount = mainMeshletBuildResults.Meshlets.Length;
            meshletCollection.MeshLODLevelCount = meshLODLevels.Length;
            meshletCollection.MeshLODLevelNodeCounts = new int[meshLODLevels.Length];
            meshletCollection.MeshLODNodes = new AAAAMeshLODNode[meshLODNodes];
            meshletCollection.Meshlets = new AAAAMeshlet[totalMeshlets];
            meshletCollection.VertexBuffer = new AAAAMeshletVertex[totalVertices];
            meshletCollection.IndexBuffer = new byte[totalIndices];

            var jobHandles = new NativeList<JobHandle>(Allocator.Temp);

            fixed (AAAAMeshLODNode* pMeshLODNodes = meshletCollection.MeshLODNodes)
            {
                fixed (AAAAMeshlet* pDestinationMeshlets = meshletCollection.Meshlets)
                {
                    fixed (AAAAMeshletVertex* pDestinationVertices = meshletCollection.VertexBuffer)
                    {
                        fixed (byte* pIndexBuffer = meshletCollection.IndexBuffer)
                        {
                            uint meshLODNodeWriteOffset = 0;
                            uint meshletsWriteOffset = 0;
                            uint verticesWriteOffset = 0;
                            uint indicesWriteOffset = 0;

                            for (int levelIndex = 0; levelIndex < meshLODLevels.Length; levelIndex++)
                            {
                                MeshLODNodeLevel level = meshLODLevels[levelIndex];

                                int levelMeshLODNodesCount = level.Groups.Length;
                                meshletCollection.MeshLODLevelNodeCounts[levelIndex] = levelMeshLODNodesCount;

                                foreach (NativeList<int> group in level.Groups)
                                {
                                    foreach (int nodeIndex in group)
                                    {
                                        if (levelIndex != 0)
                                        {
                                            Assert.IsTrue(level.Nodes[nodeIndex].Error <= level.Nodes[nodeIndex].ParentError);
                                        }
                                    }

                                    for (int index = 0; index < group.Length; index++)
                                    {
                                        int nodeIndex = group[index];
                                        MeshLODNode node = level.Nodes[nodeIndex];

                                        ref AAAAMeshLODNode thisMeshLODNode = ref pMeshLODNodes[meshLODNodeWriteOffset++];
                                        thisMeshLODNode = new AAAAMeshLODNode
                                        {
                                            MeshletCount = 1u,
                                            MeshletStartIndex = (uint) (meshletsWriteOffset + index),
                                            LevelIndex = (uint) levelIndex,
                                            Error = node.Error,
                                            Bounds = node.Bounds,
                                            ParentError = node.ParentError,
                                            ParentBounds = node.ParentBounds,
                                        };
                                    }

                                    foreach (int nodeIndex in group)
                                    {
                                        MeshLODNode node = level.Nodes[nodeIndex];

                                        MeshLODNodeLevel.MeshletNodeList meshletsNodeList = level.MeshletsNodeLists[node.MeshletNodeListIndex];

                                        AAAAMeshOptimizer.MeshletBuildResults meshletBuildResults = meshletsNodeList.MeshletBuildResults;
                                        ref readonly meshopt_Meshlet meshoptMeshlet =
                                            ref meshletBuildResults.Meshlets.ElementAtRefReadonly(node.MeshletIndex);
                                        meshopt_Bounds meshoptBounds =
                                            AAAAMeshOptimizer.ComputeMeshletBounds(meshletBuildResults, node.MeshletIndex, vertexData, vertexPositionOffset,
                                                vertexBufferStride
                                            );

                                        pDestinationMeshlets[meshletsWriteOffset++] = new AAAAMeshlet
                                        {
                                            VertexOffset = verticesWriteOffset,
                                            TriangleOffset = indicesWriteOffset,
                                            VertexCount = meshoptMeshlet.VertexCount,
                                            TriangleCount = meshoptMeshlet.TriangleCount,
                                            BoundingSphere = math.float4(meshoptBounds.Center[0], meshoptBounds.Center[1], meshoptBounds.Center[2],
                                                meshoptBounds.Radius
                                            ),
                                            ConeApexCutoff = math.float4(meshoptBounds.ConeApex[0], meshoptBounds.ConeApex[1], meshoptBounds.ConeApex[2],
                                                meshoptBounds.ConeCutoff
                                            ),
                                            ConeAxis = math.float4(meshoptBounds.ConeAxis[0], meshoptBounds.ConeAxis[1], meshoptBounds.ConeAxis[2], 0),
                                        };

                                        jobHandles.Add(new WriteVerticesJob
                                            {
                                                VerticesPtr = (byte*) vertexData.GetUnsafeReadOnlyPtr(),
                                                VertexBufferStride = vertexBufferStride,
                                                MeshletBuildResults = meshletBuildResults,
                                                MeshletIndex = node.MeshletIndex,
                                                VertexNormalOffset = (uint) data.GetVertexAttributeOffset(VertexAttribute.Normal),
                                                VertexPositionOffset = vertexPositionOffset,
                                                VertexTangentOffset = (uint) data.GetVertexAttributeOffset(VertexAttribute.Tangent),
                                                UVStreamStride = uvStreamStride,
                                                VertexUVOffset = vertexUVOffset,
                                                VerticesUVPtr = pVerticesUV,
                                                DestinationPtr = pDestinationVertices + verticesWriteOffset,
                                            }.Schedule((int) meshoptMeshlet.VertexCount, WriteVerticesJob.BatchSize)
                                        );

                                        verticesWriteOffset += meshoptMeshlet.VertexCount;

                                        uint indexCount = meshoptMeshlet.TriangleCount * 3;
                                        UnsafeUtility.MemCpy(pIndexBuffer + indicesWriteOffset,
                                            (byte*) meshletBuildResults.Indices.GetUnsafeReadOnlyPtr() + meshoptMeshlet.TriangleOffset,
                                            indexCount * sizeof(byte)
                                        );
                                        indicesWriteOffset += indexCount;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            JobHandle.CombineDependencies(jobHandles.AsArray())
                .Complete()
                ;

            if (uvVertexData.IsCreated)
            {
                uvVertexData.Dispose();
            }

            vertexData.Dispose();
            indexDataU32.Dispose();

            foreach (MeshLODNodeLevel level in meshLODLevels)
            {
                level.Dispose();
            }
        }

        private static NativeArray<uint> CastIndices16To32(NativeArray<ushort> indices, Allocator allocator)
        {
            var result = new NativeArray<uint>(indices.Length, allocator);
            for (int i = 0; i < indices.Length; i++)
            {
                result[i] = indices[i];
            }
            return result;
        }

        private static void BuildLodGraph(NativeList<MeshLODNodeLevel> levels, Allocator allocator,
            in AAAAMeshOptimizer.VertexLayout vertexLayout, AAAAMeshOptimizer.MeshletGenerationParams meshletGenerationParams, in Parameters parameters)
        {
            AAAAMeshOptimizer.SimplifyMode simplifyMode = AAAAMeshOptimizer.SimplifyMode.Normal;

            while (levels[^1].Nodes.Length > 1)
            {
                ref MeshLODNodeLevel previousLevel = ref levels.ElementAt(levels.Length - 1);
                if (previousLevel.Nodes.Length < 2)
                {
                    break;
                }

                var newLevelNodes = new NativeList<MeshLODNode>(previousLevel.Nodes.Length / 2, Allocator.TempJob);
                var meshletNodeLists = new NativeList<MeshLODNodeLevel.MeshletNodeList>(previousLevel.MeshletsNodeLists.Length / 2, Allocator.TempJob);
                uint newTriangleCount = 0;

                const int meshletsPerGroup = 4;

                NativeArray<NativeList<int>> childMeshletGroups = GroupMeshlets(previousLevel, meshletsPerGroup, Allocator.TempJob);

                for (int childGroupIndex = 0; childGroupIndex < childMeshletGroups.Length; childGroupIndex++)
                {
                    NativeList<int> sourceMeshletGroup = childMeshletGroups[childGroupIndex];
                    var sourceMeshlets = new NativeList<AAAAMeshOptimizer.MeshletBuildResults>(sourceMeshletGroup.Length, Allocator.Temp);

                    float sourceError = 0.0f;

                    float3 sourceBoundsMin = float.PositiveInfinity;
                    float3 sourceBoundsMax = float.NegativeInfinity;

                    foreach (int nodeIndex in sourceMeshletGroup)
                    {
                        MeshLODNode node = previousLevel.Nodes[nodeIndex];
                        sourceError = math.max(sourceError, node.Error);
                        AAAAMeshOptimizer.MeshletBuildResults meshletBuildResults =
                            previousLevel.MeshletsNodeLists[node.MeshletNodeListIndex].MeshletBuildResults;
                        meshletBuildResults.Meshlets = meshletBuildResults.Meshlets.GetSubArray(node.MeshletIndex, 1);
                        sourceBoundsMin = math.min(sourceBoundsMin, node.Bounds.xyz - node.Bounds.w);
                        sourceBoundsMax = math.max(sourceBoundsMin, node.Bounds.xyz + node.Bounds.w);

                        sourceMeshlets.Add(meshletBuildResults);
                    }

                    float3 sourceBoundsCenter = (sourceBoundsMin + sourceBoundsMax) * 0.5f;
                    float sourceBoundsRadius = math.length(sourceBoundsCenter - sourceBoundsMin);
                    float4 sourceBounds = math.float4(sourceBoundsCenter, sourceBoundsRadius);

                    float targetError = simplifyMode == AAAAMeshOptimizer.SimplifyMode.Sloppy ? parameters.TargetErrorSloppy : parameters.TargetError;
                    AAAAMeshOptimizer.MeshletBuildResults simplifiedMeshlets = AAAAMeshOptimizer.SimplifyMeshlets(allocator,
                        sourceMeshlets.AsArray(),
                        vertexLayout,
                        meshletGenerationParams, simplifyMode, targetError, out float localError
                    );
                    Assert.IsTrue(localError >= 0.0f);
                    sourceMeshlets.Dispose();

                    const float minSimplificationError = 0.0001f;
                    float error = sourceError + math.max(localError, minSimplificationError);
                    Assert.IsTrue(error > sourceError);

                    float4 bounds = sourceBounds;

                    // Ensure bounds are at least slightly bigger than the parents.
                    const float radiusEpsilon = 0.0001f;
                    bounds.w += radiusEpsilon;

                    for (int meshletIndex = 0; meshletIndex < simplifiedMeshlets.Meshlets.Length; meshletIndex++)
                    {
                        newTriangleCount += simplifiedMeshlets.Meshlets[meshletIndex].TriangleCount;
                        newLevelNodes.Add(new MeshLODNode
                            {
                                MeshletNodeListIndex = meshletNodeLists.Length,
                                MeshletIndex = meshletIndex,
                                ChildGroupIndex = childGroupIndex,
                                Error = error,
                                Bounds = bounds,
                            }
                        );
                    }

                    foreach (int nodeIndex in sourceMeshletGroup)
                    {
                        ref MeshLODNode childNode = ref previousLevel.Nodes.ElementAtRef(nodeIndex);
                        childNode.ParentError = error;
                        childNode.ParentBounds = bounds;
                    }

                    meshletNodeLists.Add(new MeshLODNodeLevel.MeshletNodeList
                        {
                            MeshletBuildResults = simplifiedMeshlets,
                        }
                    );
                }

                var newMeshLODNodeLevel = new MeshLODNodeLevel
                {
                    TriangleCount = newTriangleCount,
                    Nodes = newLevelNodes.AsArray(),
                    MeshletsNodeLists = meshletNodeLists.AsArray(),
                };

                previousLevel.Groups = childMeshletGroups;

                if (newTriangleCount < previousLevel.TriangleCount * parameters.MinTriangleReductionPerStep)
                {
                    levels.Add(newMeshLODNodeLevel);
                }
                else
                {
                    newMeshLODNodeLevel.Dispose();

                    if (simplifyMode == AAAAMeshOptimizer.SimplifyMode.Normal)
                    {
                        simplifyMode = AAAAMeshOptimizer.SimplifyMode.Sloppy;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // Reverse levels. Least detailed should be the first
            for (int i = 0; i < levels.Length / 2; i++)
            {
                ref MeshLODNodeLevel level1 = ref levels.ElementAtRef(i);
                ref MeshLODNodeLevel level2 = ref levels.ElementAtRef(levels.Length - 1 - i);
                (level1, level2) = (level2, level1);
            }

            for (int i = 0; i < levels.Length; i++)
            {
                ref MeshLODNodeLevel nodeLevel = ref levels.ElementAtRef(i);
                if (!nodeLevel.Groups.IsCreated)
                {
                    nodeLevel.Groups = new NativeArray<NativeList<int>>(1, Allocator.TempJob);
                    var group = new NativeList<int>(nodeLevel.Nodes.Length, Allocator.TempJob);

                    for (int nodeIndex = 0; nodeIndex < nodeLevel.Nodes.Length; nodeIndex++)
                    {
                        group.Add(nodeIndex);
                    }

                    nodeLevel.Groups[0] = group;
                }
            }

            if (parameters.MaxMeshLODLevelCount > 0)
            {
                while (levels.Length > parameters.MaxMeshLODLevelCount)
                {
                    int lastIndex = levels.Length - 1;
                    MeshLODNodeLevel level = levels[lastIndex];
                    level.Dispose();
                    levels.RemoveAt(lastIndex);
                }
            }

            {
                MeshLODNodeLevel firstLevel = levels[0];
                for (int index = 0; index < firstLevel.Nodes.Length; index++)
                {
                    ref MeshLODNode node = ref firstLevel.Nodes.ElementAtRef(index);
                    node.ParentError = -1.0f;
                    node.ParentBounds = default;
                }
            }
        }

        public struct Parameters
        {
            public Mesh Mesh;
            public string SourceMeshGUID;
            public int SubMeshIndex;
            public Action<string> LogErrorHandler;
            public bool OptimizeVertexCache;
            public int MaxMeshLODLevelCount;
            public float TargetError;
            public float TargetErrorSloppy;
            public float MinTriangleReductionPerStep;
        }

        private struct MeshLODNode : IDisposable
        {
            public int MeshletNodeListIndex;
            public int MeshletIndex;
            public int ChildGroupIndex;

            public float4 Bounds;
            public float Error;

            public float4 ParentBounds;
            public float ParentError;

            public void Dispose() { }
        }

        private struct MeshLODNodeLevel : IDisposable
        {
            public NativeArray<MeshLODNode> Nodes;
            public NativeArray<MeshletNodeList> MeshletsNodeLists;
            public NativeArray<NativeList<int>> Groups;
            public uint TriangleCount;

            public void Dispose()
            {
                foreach (MeshLODNode node in Nodes)
                {
                    node.Dispose();
                }

                foreach (MeshletNodeList meshletsNodeList in MeshletsNodeLists)
                {
                    MeshletNodeList listCopy = meshletsNodeList;
                    listCopy.MeshletBuildResults.Dispose();
                }

                MeshletsNodeLists.Dispose();
                Nodes.Dispose();
                foreach (NativeList<int> group in Groups)
                {
                    group.Dispose();
                }
                Groups.Dispose();
            }

            public struct MeshletNodeList
            {
                public AAAAMeshOptimizer.MeshletBuildResults MeshletBuildResults;
            }
        }

        [BurstCompile]
        private unsafe struct WriteVerticesJob : IJobParallelFor
        {
            public const int BatchSize = 32;

            [NativeDisableUnsafePtrRestriction]
            public byte* VerticesPtr;
            public uint VertexBufferStride;

            public uint VertexPositionOffset;
            public uint VertexNormalOffset;
            public uint VertexTangentOffset;

            [NativeDisableContainerSafetyRestriction]
            public AAAAMeshOptimizer.MeshletBuildResults MeshletBuildResults;

            [NativeDisableUnsafePtrRestriction]
            public byte* VerticesUVPtr;
            public uint UVStreamStride;
            public uint VertexUVOffset;

            [NativeDisableUnsafePtrRestriction]
            public AAAAMeshletVertex* DestinationPtr;
            public int MeshletIndex;

            public void Execute(int index)
            {
                meshopt_Meshlet meshoptMeshlet = MeshletBuildResults.Meshlets[MeshletIndex];
                int vertexOffset = (int) meshoptMeshlet.VertexOffset;
                byte* pSourceVertex = VerticesPtr + VertexBufferStride * MeshletBuildResults.Vertices[vertexOffset + index];

                var meshletVertex = new AAAAMeshletVertex
                {
                    Position = math.float4(*(float3*) (pSourceVertex + VertexPositionOffset), 1),
                    Normal = math.float4(*(float3*) (pSourceVertex + VertexNormalOffset), 0),
                    Tangent = VertexTangentOffset != uint.MaxValue ? *(float4*) (pSourceVertex + VertexTangentOffset) : default,
                };

                if (VerticesUVPtr != null)
                {
                    byte* pSourceVertexUV = VerticesUVPtr + UVStreamStride * MeshletBuildResults.Vertices[vertexOffset + index];
                    meshletVertex.UV = math.float4(*(float2*) (pSourceVertexUV + VertexUVOffset), 0, 0);
                }

                DestinationPtr[index] = meshletVertex;
            }
        }
    }
}