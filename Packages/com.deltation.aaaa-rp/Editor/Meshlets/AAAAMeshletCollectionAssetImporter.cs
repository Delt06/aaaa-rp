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
            meshletCollection.name = name;

            using (Mesh.MeshDataArray dataArray = Mesh.AcquireReadOnlyMeshData(Mesh))
            {
                Mesh.MeshData data = dataArray[0];
                uint vertexBufferStride = (uint) data.GetVertexBufferStride(0);
                NativeArray<float> vertexData = data.GetVertexData<float>();

                NativeArray<uint> indexDataU32;
                if (data.indexFormat == IndexFormat.UInt16)
                {
                    NativeArray<ushort> indexDataU16 = data.GetIndexData<ushort>();
                    indexDataU32 = CastIndices16To32(indexDataU16);
                    indexDataU16.Dispose();
                }
                else
                {
                    indexDataU32 = data.GetIndexData<uint>();
                }

                NativeArray<uint> sourceIndices = indexDataU32;
                indexDataU32 = AAAAMeshOptimizer.OptimizeVertexCache(Allocator.TempJob, sourceIndices, (uint) data.vertexCount);
                sourceIndices.Dispose();

                uint vertexPositionOffset = (uint) data.GetVertexAttributeOffset(VertexAttribute.Position);
                AAAAMeshOptimizer.MeshletGenerationParams meshletGenerationParams = AAAAMeshletCollectionAsset.MeshletGenerationParams;
                const Allocator allocator = Allocator.Temp;
                AAAAMeshOptimizer.MeshletBuildResults meshletBuildResults = AAAAMeshOptimizer.BuildMeshlets(allocator,
                    vertexData, vertexPositionOffset, vertexBufferStride, indexDataU32,
                    meshletGenerationParams
                );

                for (int i = 0; i < SimplificationSteps; i++)
                {
                    AAAAMeshOptimizer.MeshletBuildResults clusterMeshletBuildResults = AAAAMeshOptimizer.SimplifyMeshletCluster(allocator, meshletBuildResults,
                        vertexData, vertexPositionOffset, vertexBufferStride,
                        meshletGenerationParams
                    );
                    meshletBuildResults.Dispose();
                    meshletBuildResults = clusterMeshletBuildResults;
                }

                meshletCollection.Meshlets = new AAAAMeshlet[meshletBuildResults.Meshlets.Length];
                meshletCollection.VertexBuffer = new AAAAMeshletVertex[meshletBuildResults.Vertices.Length];

                fixed (AAAAMeshlet* pDestinationMeshlets = meshletCollection.Meshlets)
                {
                    fixed (AAAAMeshletVertex* pDestinationVertices = meshletCollection.VertexBuffer)
                    {
                        int uvStream = data.GetVertexAttributeStream(VertexAttribute.TexCoord0);
                        int uvStreamStride = uvStream >= 0 ? data.GetVertexBufferStride(uvStream) : 0;
                        NativeArray<float> uvVertexData = uvStream >= 0 ? data.GetVertexData<float>(uvStream) : default;
                        byte* pVerticesUV = uvVertexData.IsCreated ? (byte*) uvVertexData.GetUnsafeReadOnlyPtr() : null;
                        int vertexUVOffset = data.GetVertexAttributeOffset(VertexAttribute.TexCoord0);

                        var jobHandles = new NativeList<JobHandle>(Allocator.Temp)
                        {
                            new WriteMeshletsJob
                            {
                                DestinationPtr = pDestinationMeshlets,
                                VertexBufferStride = vertexBufferStride,
                                VertexPositionOffset = vertexPositionOffset,
                                MeshletBuildResults = meshletBuildResults,
                                VertexData = vertexData,
                            }.Schedule(meshletCollection.Meshlets.Length, WriteMeshletsJob.BatchSize),
                            new WriteVerticesJob
                            {
                                VerticesPtr = (byte*) vertexData.GetUnsafeReadOnlyPtr(),
                                VertexBufferStride = vertexBufferStride,
                                MeshletBuildResults = meshletBuildResults,
                                VertexNormalOffset = (uint) data.GetVertexAttributeOffset(VertexAttribute.Normal),
                                VertexPositionOffset = vertexPositionOffset,
                                VertexTangentOffset = (uint) data.GetVertexAttributeOffset(VertexAttribute.Tangent),
                                UVStreamStride = (uint) uvStreamStride,
                                VertexUVOffset = (uint) vertexUVOffset,
                                VerticesUVPtr = pVerticesUV,
                                DestinationPtr = pDestinationVertices,
                            }.Schedule(meshletCollection.VertexBuffer.Length, WriteVerticesJob.BatchSize),
                        };

                        meshletCollection.IndexBuffer = new byte[meshletBuildResults.Indices.Length];
                        fixed (byte* pIndexBuffer = meshletCollection.IndexBuffer)
                        {
                            UnsafeUtility.MemCpy(pIndexBuffer, meshletBuildResults.Indices.GetUnsafeReadOnlyPtr(), meshletBuildResults.Indices.Length);
                        }

                        var jobHandle = JobHandle.CombineDependencies(jobHandles.AsArray());

                        if (uvVertexData.IsCreated)
                        {
                            uvVertexData.Dispose(jobHandle);
                        }

                        vertexData.Dispose(jobHandle);
                        indexDataU32.Dispose(jobHandle);
                        meshletBuildResults.Dispose(jobHandle);

                        jobHandle.Complete();
                    }
                }
            }

            ctx.AddObjectToAsset(nameof(AAAAMeshletCollectionAsset), meshletCollection);
            ctx.SetMainObject(meshletCollection);
        }

        private static NativeArray<uint> CastIndices16To32(NativeArray<ushort> indices)
        {
            var result = new NativeArray<uint>(indices.Length, Allocator.Temp);
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

            public void Execute(int index)
            {
                ref readonly meshopt_Meshlet meshoptMeshlet = ref MeshletBuildResults.Meshlets.ElementAtRefReadonly(index);
                meshopt_Bounds meshoptBounds =
                    AAAAMeshOptimizer.ComputeMeshletBounds(MeshletBuildResults, index, VertexData, VertexPositionOffset, VertexBufferStride);

                DestinationPtr[index] = new AAAAMeshlet
                {
                    VertexOffset = meshoptMeshlet.VertexOffset,
                    TriangleOffset = meshoptMeshlet.TriangleOffset,
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