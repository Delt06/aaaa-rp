using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.Meshlets;
using DELTation.AAAARP.MeshOptimizer.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Editor.Meshlets
{
    [ScriptedImporter(1, Extension)]
    internal partial class AAAAMeshletCollectionAssetImporter : ScriptedImporter
    {
        private const string Extension = "aaaameshletcollection";

        public Mesh Mesh;
        public bool OptimizeIndexing;
        public bool OptimizeVertexCache;
        [Min(0)]
        public int MaxStepsCount = 100;

        public override unsafe void OnImportAsset(AssetImportContext ctx)
        {
            if (Mesh == null)
            {
                return;
            }

            ctx.DependsOnArtifact(AssetDatabase.GetAssetPath(Mesh));

            AAAAMeshletCollectionAsset meshletCollection = ScriptableObject.CreateInstance<AAAAMeshletCollectionAsset>();
            meshletCollection.Bounds = Mesh.bounds;
            meshletCollection.name = name;

            using (Mesh.MeshDataArray dataArray = Mesh.AcquireReadOnlyMeshData(Mesh))
            {
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

                int uvStream = data.GetVertexAttributeStream(VertexAttribute.TexCoord0);
                uint uvStreamStride = (uint) (uvStream >= 0 ? data.GetVertexBufferStride(uvStream) : 0);
                NativeArray<float> uvVertexData = uvStream >= 0 ? data.GetVertexData<float>(uvStream) : default;
                byte* pVerticesUV = uvVertexData.IsCreated ? (byte*) uvVertexData.GetUnsafeReadOnlyPtr() : null;
                int vertexUVOffset = data.GetVertexAttributeOffset(VertexAttribute.TexCoord0);

                uint vertexCount = (uint) data.vertexCount;

                if (OptimizeIndexing)
                {
                    var streams = new NativeList<meshopt_Stream>(Allocator.Temp);

                    var copiedVertices = new NativeArray<float>(vertexData.Length, Allocator.TempJob);
                    copiedVertices.CopyFrom(vertexData);
                    vertexData.Dispose();
                    vertexData = copiedVertices;

                    streams.Add(new meshopt_Stream
                        {
                            data = (byte*) vertexData.GetUnsafePtr() + vertexPositionOffset,
                            size = vertexBufferStride,
                            stride = vertexBufferStride,
                        }
                    );

                    if (uvStream > 0)
                    {
                        var copiedUVs = new NativeArray<float>(uvVertexData.Length, Allocator.TempJob);
                        copiedUVs.CopyFrom(uvVertexData);
                        uvVertexData.Dispose();
                        uvVertexData = copiedUVs;

                        streams.Add(new meshopt_Stream
                            {
                                data = (byte*) uvVertexData.GetUnsafePtr() + vertexUVOffset,
                                size = uvStreamStride,
                                stride = uvStreamStride,
                            }
                        );
                    }

                    var copiedIndices = new NativeArray<uint>(indexDataU32.Length, Allocator.TempJob);
                    copiedIndices.CopyFrom(indexDataU32);
                    indexDataU32.Dispose();
                    indexDataU32 = copiedIndices;

                    uint newVertexCount = AAAAMeshOptimizer.OptimizeIndexingInPlace(vertexCount, indexDataU32, streams.AsArray());
                    vertexCount = newVertexCount;

                    vertexData = vertexData.GetSubArray(0, (int) (newVertexCount * (vertexBufferStride / sizeof(float))));

                    if (uvVertexData.IsCreated)
                    {
                        uvVertexData = uvVertexData.GetSubArray(0, (int) (newVertexCount * (uvStreamStride / sizeof(float))));
                    }
                }

                if (OptimizeVertexCache)
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
                        [0] = new()
                        {
                            MeshletBuildResults = mainMeshletBuildResults,
                            NodeIndices = new int2(0, mainMeshletBuildResults.Meshlets.Length),
                        },
                    },
                };

                for (int i = 0; i < topLOD.Nodes.Length; ++i)
                {
                    topLOD.TriangleCount += topLOD.MeshletsNodeLists[0].MeshletBuildResults.Meshlets[i].TriangleCount;
                    topLOD.Nodes[i] = new MeshLODNode
                    {
                        MeshletNodeListIndex = 0,
                        MeshletIndex = i,
                        ChildrenNodeIndices = new NativeList<int>(0, Allocator.TempJob),
                    };
                }
                meshLODLevels.Add(topLOD);

                BuildLodGraph(meshLODLevels, allocator, vertexData, vertexPositionOffset, vertexBufferStride, meshletGenerationParams);

                int totalMeshlets = 0;
                int totalVertices = 0;
                int totalIndices = 0;

                foreach (MeshLODNodeLevel level in meshLODLevels)
                {
                    foreach (MeshLODNodeLevel.MeshletNodeList meshletNodeList in level.MeshletsNodeLists)
                    {
                        totalMeshlets += meshletNodeList.MeshletBuildResults.Meshlets.Length;
                        totalVertices += meshletNodeList.MeshletBuildResults.Vertices.Length;
                        totalIndices += meshletNodeList.MeshletBuildResults.Indices.Length;
                    }
                }

                meshletCollection.Meshlets = new AAAAMeshlet[totalMeshlets];
                meshletCollection.VertexBuffer = new AAAAMeshletVertex[totalVertices];
                meshletCollection.IndexBuffer = new byte[totalIndices];

                var jobHandles = new NativeList<JobHandle>(Allocator.Temp);

                fixed (AAAAMeshlet* pDestinationMeshlets = meshletCollection.Meshlets)
                {
                    fixed (AAAAMeshletVertex* pDestinationVertices = meshletCollection.VertexBuffer)
                    {
                        fixed (byte* pIndexBuffer = meshletCollection.IndexBuffer)
                        {
                            int meshletsWriteOffset = 0;
                            int verticesWriteOffset = 0;
                            int indicesWriteOffset = 0;

                            for (int levelIndex = 0; levelIndex < meshLODLevels.Length; levelIndex++)
                            {
                                MeshLODNodeLevel level = meshLODLevels[levelIndex];

                                int totalMeshletCountForThisLevel = 0;

                                foreach (MeshLODNodeLevel.MeshletNodeList meshletNodeList in level.MeshletsNodeLists)
                                {
                                    totalMeshletCountForThisLevel += meshletNodeList.MeshletBuildResults.Meshlets.Length;
                                }

                                if (levelIndex == 0)
                                {
                                    meshletCollection.TopMeshletCount = totalMeshletCountForThisLevel;
                                }

                                uint childrenOffset = (uint) (meshletsWriteOffset + totalMeshletCountForThisLevel);

                                foreach (MeshLODNodeLevel.MeshletNodeList meshletNodeList in level.MeshletsNodeLists)
                                {
                                    AAAAMeshOptimizer.MeshletBuildResults meshletBuildResults = meshletNodeList.MeshletBuildResults;

                                    int meshletCount = meshletBuildResults.Meshlets.Length;
                                    int2 nodeIndicesRange = meshletNodeList.NodeIndices;
                                    MeshLODNode firstNode = level.Nodes[nodeIndicesRange.x];

                                    AAAAMeshlet* pThisDestinationMeshlets = pDestinationMeshlets + meshletsWriteOffset;
                                    JobHandle writeMeshletsJob = new WriteMeshletsJob
                                    {
                                        DestinationPtr = pThisDestinationMeshlets,
                                        VertexBufferStride = vertexBufferStride,
                                        VertexPositionOffset = vertexPositionOffset,
                                        MeshletBuildResults = meshletBuildResults,
                                        VertexData = vertexData,
                                        VertexOffset = (uint) verticesWriteOffset,
                                        TriangleOffset = (uint) indicesWriteOffset,
                                        ChildrenNodeIndices = firstNode.ChildrenNodeIndices.AsArray(),
                                        ChildrenOffset = childrenOffset,
                                    }.Schedule(meshletCount, WriteMeshletsJob.BatchSize);
                                    jobHandles.Add(writeMeshletsJob);

                                    jobHandles.Add(new WriteVerticesJob
                                        {
                                            VerticesPtr = (byte*) vertexData.GetUnsafeReadOnlyPtr(),
                                            VertexBufferStride = vertexBufferStride,
                                            MeshletBuildResults = meshletBuildResults,
                                            VertexNormalOffset = (uint) data.GetVertexAttributeOffset(VertexAttribute.Normal),
                                            VertexPositionOffset = vertexPositionOffset,
                                            VertexTangentOffset = (uint) data.GetVertexAttributeOffset(VertexAttribute.Tangent),
                                            UVStreamStride = uvStreamStride,
                                            VertexUVOffset = (uint) vertexUVOffset,
                                            VerticesUVPtr = pVerticesUV,
                                            DestinationPtr = pDestinationVertices + verticesWriteOffset,
                                        }.Schedule(meshletBuildResults.Vertices.Length, WriteVerticesJob.BatchSize)
                                    );


                                    UnsafeUtility.MemCpy(pIndexBuffer + indicesWriteOffset, meshletBuildResults.Indices.GetUnsafeReadOnlyPtr(),
                                        meshletBuildResults.Indices.Length * sizeof(byte)
                                    );

                                    meshletsWriteOffset += meshletCount;
                                    verticesWriteOffset += meshletBuildResults.Vertices.Length;
                                    indicesWriteOffset += meshletBuildResults.Indices.Length;
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

            ctx.AddObjectToAsset(nameof(AAAAMeshletCollectionAsset), meshletCollection);
            ctx.SetMainObject(meshletCollection);
        }

        private void BuildLodGraph(NativeList<MeshLODNodeLevel> levels, Allocator allocator, NativeArray<float> vertexData, uint vertexPositionOffset,
            uint vertexBufferStride, AAAAMeshOptimizer.MeshletGenerationParams meshletGenerationParams)
        {
            int stepIndex = 0;
            while (levels[^1].Nodes.Length > 1)
            {
                ++stepIndex;
                if (stepIndex > MaxStepsCount)
                {
                    break;
                }

                MeshLODNodeLevel previousLevel = levels[^1];
                var newLevelNodes = new NativeList<MeshLODNode>(previousLevel.Nodes.Length / 2, Allocator.TempJob);
                var meshletNodeLists = new NativeList<MeshLODNodeLevel.MeshletNodeList>(previousLevel.MeshletsNodeLists.Length / 2, Allocator.TempJob);
                uint newTriangleCount = 0;

                const int meshletsPerGroup = 4;

                NativeArray<NativeList<int>> meshletGroups = GroupMeshlets(previousLevel, meshletsPerGroup, Allocator.Temp);

                foreach (NativeList<int> meshletGroup in meshletGroups)
                {
                    var sourceMeshlets = new NativeList<AAAAMeshOptimizer.MeshletBuildResults>(meshletGroup.Length, Allocator.Temp);

                    foreach (int nodeIndex in meshletGroup)
                    {
                        MeshLODNode node = previousLevel.Nodes[nodeIndex];
                        AAAAMeshOptimizer.MeshletBuildResults meshletBuildResults =
                            previousLevel.MeshletsNodeLists[node.MeshletNodeListIndex].MeshletBuildResults;
                        meshletBuildResults.Meshlets = meshletBuildResults.Meshlets.GetSubArray(node.MeshletIndex, 1);
                        sourceMeshlets.Add(meshletBuildResults);
                    }

                    AAAAMeshOptimizer.MeshletBuildResults simplifiedMeshlets = AAAAMeshOptimizer.SimplifyMeshlets(allocator,
                        sourceMeshlets.AsArray(),
                        vertexData, vertexPositionOffset, vertexBufferStride,
                        meshletGenerationParams
                    );
                    sourceMeshlets.Dispose();

                    var nodeIndices = new int2(newLevelNodes.Length, newLevelNodes.Length + simplifiedMeshlets.Meshlets.Length);

                    for (int meshletIndex = 0; meshletIndex < simplifiedMeshlets.Meshlets.Length; meshletIndex++)
                    {
                        newTriangleCount += simplifiedMeshlets.Meshlets[meshletIndex].TriangleCount;
                        var childrenNodeIndices = new NativeList<int>(meshletGroup.Length, allocator);
                        childrenNodeIndices.CopyFrom(meshletGroup);
                        newLevelNodes.Add(new MeshLODNode
                            {
                                MeshletNodeListIndex = meshletNodeLists.Length,
                                MeshletIndex = meshletIndex,
                                ChildrenNodeIndices = childrenNodeIndices,
                            }
                        );
                    }

                    meshletNodeLists.Add(new MeshLODNodeLevel.MeshletNodeList
                        {
                            NodeIndices = nodeIndices,
                            MeshletBuildResults = simplifiedMeshlets,
                        }
                    );
                }

                meshletGroups.Dispose();

                var newMeshLODNodeLevel = new MeshLODNodeLevel
                {
                    TriangleCount = newTriangleCount,
                    Nodes = newLevelNodes.AsArray(),
                    MeshletsNodeLists = meshletNodeLists.AsArray(),
                };

                if (newTriangleCount < previousLevel.TriangleCount)
                {
                    levels.Add(newMeshLODNodeLevel);
                }
                else
                {
                    newMeshLODNodeLevel.Dispose();
                    break;
                }
            }

            // Reverse levels. Least detailed should be the first
            for (int i = 0; i < levels.Length / 2; i++)
            {
                ref MeshLODNodeLevel level1 = ref levels.ElementAtRef(i);
                ref MeshLODNodeLevel level2 = ref levels.ElementAtRef(levels.Length - 1 - i);
                (level1, level2) = (level2, level1);
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

        [MenuItem("Assets/Create/AAAA RP/Meshlet Collection")]
        public static void CreateNewAsset(MenuCommand menuCommand)
        {
            Mesh mesh = Selection.objects.OfType<Mesh>().FirstOrDefault();
            if (mesh != null)
            {
                string path = AssetDatabase.GetAssetPath(mesh);
                string folder = File.Exists(path) ? Path.GetDirectoryName(path) : path;

                string fileName = mesh.name + "_Meshlets." + Extension;
                string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folder ?? "Assets", fileName));

                File.WriteAllText(assetPath, string.Empty);
                AssetDatabase.Refresh();

                var assetImporter = (AAAAMeshletCollectionAssetImporter) GetAtPath(assetPath);
                assetImporter.Mesh = mesh;
                Save(assetPath, assetImporter);
            }
            else
            {
                ProjectWindowUtil.CreateAssetWithContent("New Meshlet Collection." + Extension, string.Empty);
            }
        }

        private static async void Save(string assetPath, AAAAMeshletCollectionAssetImporter importer)
        {
            EditorUtility.SetDirty(importer);
            AssetDatabase.SaveAssetIfDirty(importer);

            await Task.Yield();

            importer.SaveAndReimport();

            AAAAMeshletCollectionAsset meshletCollection = AssetDatabase.LoadAssetAtPath<AAAAMeshletCollectionAsset>(assetPath);
            Selection.activeObject = meshletCollection;
        }

        private struct MeshLODNode : IDisposable
        {
            public int MeshletNodeListIndex;
            public int MeshletIndex;
            public NativeList<int> ChildrenNodeIndices;

            public void Dispose()
            {
                ChildrenNodeIndices.Dispose();
            }
        }

        private struct MeshLODNodeLevel : IDisposable
        {
            public NativeArray<MeshLODNode> Nodes;
            public NativeArray<MeshletNodeList> MeshletsNodeLists;
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
            }

            public struct MeshletNodeList
            {
                public AAAAMeshOptimizer.MeshletBuildResults MeshletBuildResults;
                public int2 NodeIndices;
            }
        }

        private unsafe struct WriteMeshletsJob : IJobParallelFor
        {
            public const int BatchSize = 32;

            [NativeDisableContainerSafetyRestriction]
            public AAAAMeshOptimizer.MeshletBuildResults MeshletBuildResults;
            [ReadOnly]
            public NativeArray<float> VertexData;

            public uint VertexPositionOffset;
            public uint VertexBufferStride;

            [NativeDisableUnsafePtrRestriction]
            public AAAAMeshlet* DestinationPtr;

            public uint VertexOffset;
            public uint TriangleOffset;

            [ReadOnly]
            public NativeArray<int> ChildrenNodeIndices;
            public uint ChildrenOffset;

            public void Execute(int index)
            {
                ref readonly meshopt_Meshlet meshoptMeshlet = ref MeshletBuildResults.Meshlets.ElementAtRefReadonly(index);
                meshopt_Bounds meshoptBounds =
                    AAAAMeshOptimizer.ComputeMeshletBounds(MeshletBuildResults, index, VertexData, VertexPositionOffset, VertexBufferStride);

                var meshlet = new AAAAMeshlet
                {
                    VertexOffset = meshoptMeshlet.VertexOffset + VertexOffset,
                    TriangleOffset = meshoptMeshlet.TriangleOffset + TriangleOffset,
                    VertexCount = meshoptMeshlet.VertexCount,
                    TriangleCount = meshoptMeshlet.TriangleCount,
                    BoundingSphere = math.float4(meshoptBounds.Center[0], meshoptBounds.Center[1], meshoptBounds.Center[2], meshoptBounds.Radius),
                    ConeApexCutoff = math.float4(meshoptBounds.ConeApex[0], meshoptBounds.ConeApex[1], meshoptBounds.ConeApex[2], meshoptBounds.ConeCutoff),
                    ConeAxis = math.float4(meshoptBounds.ConeAxis[0], meshoptBounds.ConeAxis[1], meshoptBounds.ConeAxis[2], 0),
                };
                CollectChildrenNodeIndices(ref meshlet, ChildrenNodeIndices);
                meshlet.AddChildrenOffset(ChildrenOffset);
                DestinationPtr[index] = meshlet;
            }

            private static void CollectChildrenNodeIndices(ref AAAAMeshlet meshlet, NativeArray<int> indexList)
            {
                Assert.IsTrue(indexList.Length <= AAAAMeshlet.ChildrenCount);

                for (int i = 0; i < AAAAMeshlet.ChildrenCount; i++)
                {
                    if (i < indexList.Length)
                    {
                        meshlet.ChildrenNodeIndices[i] = (uint) indexList[i];
                    }
                    else
                    {
                        meshlet.ChildrenNodeIndices[i] = AAAAMeshlet.InvalidChildIndex;
                    }
                }
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

            public void Execute(int index)
            {
                byte* pSourceVertex = VerticesPtr + VertexBufferStride * MeshletBuildResults.Vertices[index];

                var meshletVertex = new AAAAMeshletVertex
                {
                    Position = math.float4(*(float3*) (pSourceVertex + VertexPositionOffset), 1),
                    Normal = math.float4(*(float3*) (pSourceVertex + VertexNormalOffset), 0),
                    Tangent = VertexTangentOffset != uint.MaxValue ? *(float4*) (pSourceVertex + VertexTangentOffset) : default,
                };

                if (VerticesUVPtr != null)
                {
                    byte* pSourceVertexUV = VerticesUVPtr + UVStreamStride * MeshletBuildResults.Vertices[index];
                    meshletVertex.UV = math.float4(*(float2*) (pSourceVertexUV + VertexUVOffset), 0, 0);
                }

                DestinationPtr[index] = meshletVertex;
            }
        }
    }
}