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
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Editor.Meshlets
{
    [ScriptedImporter(1, Extension)]
    internal class AAAAMeshletCollectionAssetImporter : ScriptedImporter
    {
        private const string Extension = "aaaameshletcollection";

        public Mesh Mesh;
        public bool OptimizeIndexing;
        public bool OptimizeVertexCache;
        public bool MeshletSpatialSort;
        [Min(0)]
        public int SimplificationSteps;

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
                const Allocator allocator = Allocator.Temp;
                AAAAMeshOptimizer.MeshletBuildResults mainMeshletBuildResults = AAAAMeshOptimizer.BuildMeshlets(allocator,
                    vertexData, vertexPositionOffset, vertexBufferStride, indexDataU32,
                    meshletGenerationParams
                );
                if (MeshletSpatialSort)
                {
                    SpatialMeshletSort(ref mainMeshletBuildResults, vertexData, vertexPositionOffset, vertexBufferStride);
                }

                for (int i = 0; i < SimplificationSteps; i++)
                {
                    AAAAMeshOptimizer.MeshletBuildResults clusterMeshletBuildResults = AAAAMeshOptimizer.SimplifyMeshletCluster(allocator,
                        mainMeshletBuildResults,
                        vertexData, vertexPositionOffset, vertexBufferStride,
                        meshletGenerationParams
                    );

                    if (MeshletSpatialSort)
                    {
                        SpatialMeshletSort(ref clusterMeshletBuildResults, vertexData, vertexPositionOffset, vertexBufferStride);
                    }

                    mainMeshletBuildResults.Dispose();
                    mainMeshletBuildResults = clusterMeshletBuildResults;
                }

                var lodMeshlets = new NativeList<AAAAMeshOptimizer.MeshletBuildResults>((int) AAAAMeshletConfiguration.LodCount, Allocator.TempJob);
                lodMeshlets.Add(mainMeshletBuildResults);

                for (int i = 1; i < (int) AAAAMeshletConfiguration.LodCount; i++)
                {
                    AAAAMeshOptimizer.MeshletBuildResults previousLodMeshlets = lodMeshlets[^1];
                    AAAAMeshOptimizer.MeshletBuildResults thisLodMeshlets = AAAAMeshOptimizer.SimplifyMeshletCluster(allocator, previousLodMeshlets,
                        vertexData, vertexPositionOffset, vertexBufferStride,
                        meshletGenerationParams
                    );

                    const int meshletCountDifferenceThreshold = 2;
                    if (thisLodMeshlets.Meshlets.Length >= previousLodMeshlets.Meshlets.Length - meshletCountDifferenceThreshold)
                    {
                        thisLodMeshlets.Dispose();
                        break;
                    }

                    if (MeshletSpatialSort)
                    {
                        SpatialMeshletSort(ref thisLodMeshlets, vertexData, vertexPositionOffset, vertexBufferStride);
                    }

                    lodMeshlets.Add(thisLodMeshlets);
                }

                int totalMeshlets = 0;
                int totalVertices = 0;
                int totalIndices = 0;

                meshletCollection.Lods = new AAAAMeshLOD[lodMeshlets.Length];

                foreach (AAAAMeshOptimizer.MeshletBuildResults buildResults in lodMeshlets)
                {
                    totalMeshlets += buildResults.Meshlets.Length;
                    totalVertices += buildResults.Vertices.Length;
                    totalIndices += buildResults.Indices.Length;
                }

                meshletCollection.Meshlets = new AAAAMeshlet[totalMeshlets];
                meshletCollection.VertexBuffer = new AAAAMeshletVertex[totalVertices];
                meshletCollection.IndexBuffer = new byte[totalIndices];

                int meshletsWriteOffset = 0;
                int verticesWriteOffset = 0;
                int indicesWriteOffset = 0;

                var jobHandles = new NativeList<JobHandle>(Allocator.Temp);

                fixed (AAAAMeshlet* pDestinationMeshlets = meshletCollection.Meshlets)
                {
                    fixed (AAAAMeshletVertex* pDestinationVertices = meshletCollection.VertexBuffer)
                    {
                        fixed (byte* pIndexBuffer = meshletCollection.IndexBuffer)
                        {
                            for (int index = 0; index < lodMeshlets.Length; index++)
                            {
                                AAAAMeshOptimizer.MeshletBuildResults lodBuildResults = lodMeshlets[index];

                                meshletCollection.Lods[index] = new AAAAMeshLOD
                                {
                                    MeshletStartOffset = (uint) meshletsWriteOffset,
                                    MeshletCount = (uint) lodBuildResults.Meshlets.Length,
                                };

                                jobHandles.Add(new WriteMeshletsJob
                                    {
                                        DestinationPtr = pDestinationMeshlets + meshletsWriteOffset,
                                        VertexBufferStride = vertexBufferStride,
                                        VertexPositionOffset = vertexPositionOffset,
                                        MeshletBuildResults = lodBuildResults,
                                        VertexData = vertexData,
                                        VertexOffset = (uint) verticesWriteOffset,
                                        TriangleOffset = (uint) indicesWriteOffset,
                                    }.Schedule(lodBuildResults.Meshlets.Length, WriteMeshletsJob.BatchSize)
                                );
                                jobHandles.Add(new WriteVerticesJob
                                    {
                                        VerticesPtr = (byte*) vertexData.GetUnsafeReadOnlyPtr(),
                                        VertexBufferStride = vertexBufferStride,
                                        MeshletBuildResults = lodBuildResults,
                                        VertexNormalOffset = (uint) data.GetVertexAttributeOffset(VertexAttribute.Normal),
                                        VertexPositionOffset = vertexPositionOffset,
                                        VertexTangentOffset = (uint) data.GetVertexAttributeOffset(VertexAttribute.Tangent),
                                        UVStreamStride = uvStreamStride,
                                        VertexUVOffset = (uint) vertexUVOffset,
                                        VerticesUVPtr = pVerticesUV,
                                        DestinationPtr = pDestinationVertices + verticesWriteOffset,
                                    }.Schedule(lodBuildResults.Vertices.Length, WriteVerticesJob.BatchSize)
                                );


                                UnsafeUtility.MemCpy(pIndexBuffer + indicesWriteOffset, lodBuildResults.Indices.GetUnsafeReadOnlyPtr(),
                                    lodBuildResults.Indices.Length * sizeof(byte)
                                );

                                meshletsWriteOffset += lodBuildResults.Meshlets.Length;
                                verticesWriteOffset += lodBuildResults.Vertices.Length;
                                indicesWriteOffset += lodBuildResults.Indices.Length;
                            }
                        }
                    }
                }

                var jobHandle = JobHandle.CombineDependencies(jobHandles.AsArray());

                if (uvVertexData.IsCreated)
                {
                    uvVertexData.Dispose(jobHandle);
                }

                vertexData.Dispose(jobHandle);
                indexDataU32.Dispose(jobHandle);

                foreach (AAAAMeshOptimizer.MeshletBuildResults meshlets in lodMeshlets)
                {
                    meshlets.Dispose(jobHandle);
                }

                jobHandle.Complete();
            }

            ctx.AddObjectToAsset(nameof(AAAAMeshletCollectionAsset), meshletCollection);
            ctx.SetMainObject(meshletCollection);
        }

        private unsafe void SpatialMeshletSort(ref AAAAMeshOptimizer.MeshletBuildResults meshletBuildResults, NativeArray<float> vertexData,
            uint vertexPositionOffset, uint vertexPositionStride)
        {
            int meshletCount = meshletBuildResults.Meshlets.Length;
            var sortPositions = new NativeArray<float3>(meshletCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);


            for (int meshletIndex = 0; meshletIndex < meshletCount; meshletIndex++)
            {
                meshopt_Bounds meshoptBounds =
                    AAAAMeshOptimizer.ComputeMeshletBounds(meshletBuildResults, meshletIndex, vertexData, vertexPositionOffset, vertexPositionStride);
                sortPositions[meshletIndex] = *(float3*) meshoptBounds.Center;
            }

            NativeArray<meshopt_Meshlet> sortedMeshlets = AAAAMeshOptimizer.SpatialSort(meshletBuildResults.Meshlets, sortPositions, Allocator.TempJob);
            meshletBuildResults.Meshlets.Dispose();
            meshletBuildResults.Meshlets = sortedMeshlets;
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

            public void Execute(int index)
            {
                ref readonly meshopt_Meshlet meshoptMeshlet = ref MeshletBuildResults.Meshlets.ElementAtRefReadonly(index);
                meshopt_Bounds meshoptBounds =
                    AAAAMeshOptimizer.ComputeMeshletBounds(MeshletBuildResults, index, VertexData, VertexPositionOffset, VertexBufferStride);

                DestinationPtr[index] = new AAAAMeshlet
                {
                    VertexOffset = meshoptMeshlet.VertexOffset + VertexOffset,
                    TriangleOffset = meshoptMeshlet.TriangleOffset + TriangleOffset,
                    VertexCount = meshoptMeshlet.VertexCount,
                    TriangleCount = meshoptMeshlet.TriangleCount,
                    BoundingSphere = math.float4(meshoptBounds.Center[0], meshoptBounds.Center[1], meshoptBounds.Center[2], meshoptBounds.Radius),
                    ConeApexCutoff = math.float4(meshoptBounds.ConeApex[0], meshoptBounds.ConeApex[1], meshoptBounds.ConeApex[2], meshoptBounds.ConeCutoff),
                    ConeAxis = math.float4(meshoptBounds.ConeAxis[0], meshoptBounds.ConeAxis[1], meshoptBounds.ConeAxis[2], 0),
                };
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
                    Tangent = *(float4*) (pSourceVertex + VertexTangentOffset),
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