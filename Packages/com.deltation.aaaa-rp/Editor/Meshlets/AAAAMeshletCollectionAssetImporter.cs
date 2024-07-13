using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.Meshlets;
using DELTation.AAAARP.MeshOptimizer.Runtime;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
                int vertexBufferStride = data.GetVertexBufferStride(0);
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

                int vertexPositionOffset = data.GetVertexAttributeOffset(VertexAttribute.Position);
                AAAAMeshOptimizer.MeshletBuildResults meshletBuildResults = AAAAMeshOptimizer.BuildMeshlets(Allocator.Temp, vertexData,
                    (uint) vertexPositionOffset,
                    (uint) vertexBufferStride, indexDataU32,
                    AAAAMeshletCollectionAsset.MeshletGenerationParams
                );

                meshletCollection.Meshlets = new AAAAMeshlet[meshletBuildResults.Meshlets.Length];

                for (int i = 0; i < meshletBuildResults.Meshlets.Length; i++)
                {
                    ref readonly meshopt_Meshlet meshoptMeshlet = ref meshletBuildResults.Meshlets.ElementAtRefReadonly(i);
                    meshopt_Bounds meshoptBounds =
                        AAAAMeshOptimizer.ComputeMeshletBounds(meshletBuildResults, i, vertexData, (uint) vertexPositionOffset, (uint) vertexBufferStride);

                    meshletCollection.Meshlets[i] = new AAAAMeshlet
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

                meshletCollection.VertexBuffer = new AAAAMeshletVertex[meshletBuildResults.Vertices.Length];

                byte* pVertices = (byte*) vertexData.GetUnsafeReadOnlyPtr();
                int vertexNormalOffset = data.GetVertexAttributeOffset(VertexAttribute.Normal);
                int vertexTangentOffset = data.GetVertexAttributeOffset(VertexAttribute.Tangent);

                int uvStream = data.GetVertexAttributeStream(VertexAttribute.TexCoord0);
                int uvStreamStride = uvStream >= 0 ? data.GetVertexBufferStride(uvStream) : 0;
                NativeArray<float> uvVertexData = uvStream >= 0 ? data.GetVertexData<float>(uvStream) : default;
                byte* pVerticesUV = uvVertexData.IsCreated ? (byte*) uvVertexData.GetUnsafeReadOnlyPtr() : null;
                int vertexUVOffset = data.GetVertexAttributeOffset(VertexAttribute.TexCoord0);

                for (int i = 0; i < meshletBuildResults.Vertices.Length; i++)
                {
                    byte* pVertex = pVertices + vertexBufferStride * meshletBuildResults.Vertices[i];

                    var meshletVertex = new AAAAMeshletVertex
                    {
                        Position = math.float4(*(float3*) (pVertex + vertexPositionOffset), 1),
                        Normal = math.float4(*(float3*) (pVertex + vertexNormalOffset), 0),
                        Tangent = *(float4*) (pVertex + vertexTangentOffset),
                    };

                    if (uvStream >= 0)
                    {
                        byte* pVertexUV = pVerticesUV + uvStreamStride * meshletBuildResults.Vertices[i];
                        meshletVertex.UV = math.float4(*(float2*) (pVertexUV + vertexUVOffset), 0, 0);
                    }

                    meshletCollection.VertexBuffer[i] = meshletVertex;
                }

                if (uvVertexData.IsCreated)
                    uvVertexData.Dispose();

                meshletCollection.IndexBuffer = new byte[meshletBuildResults.Indices.Length];
                fixed (byte* pIndexBuffer = meshletCollection.IndexBuffer)
                {
                    UnsafeUtility.MemCpy(pIndexBuffer, meshletBuildResults.Indices.GetUnsafeReadOnlyPtr(), meshletBuildResults.Indices.Length);
                }

                indexDataU32.Dispose();

                vertexData.Dispose();
                indexDataU32.Dispose();

                meshletBuildResults.Dispose();
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
    }
}