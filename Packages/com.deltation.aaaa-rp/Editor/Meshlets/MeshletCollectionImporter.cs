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
    public class MeshletCollectionImporter : ScriptedImporter
    {
        public const string Extension = "meshletcollection";
        
        public Mesh Mesh;
        
        public override unsafe void OnImportAsset(AssetImportContext ctx)
        {
            if (Mesh == null)
            {
                return;
            }
            
            ctx.DependsOnArtifact(AssetDatabase.GetAssetPath(Mesh));
            if (Mesh.indexFormat != IndexFormat.UInt16)
            {
                ctx.LogImportError("Only UInt16 index format is supported.", this);
                return;
            }
            
            MeshletCollection meshletCollection = ScriptableObject.CreateInstance<MeshletCollection>();
            meshletCollection.name = name;
            
            using (Mesh.MeshDataArray dataArray = Mesh.AcquireReadOnlyMeshData(Mesh))
            {
                Mesh.MeshData data = dataArray[0];
                int vertexBufferStride = data.GetVertexBufferStride(0);
                NativeArray<float> vertexData = data.GetVertexData<float>();
                NativeArray<ushort> indexData = data.GetIndexData<ushort>();
                
                NativeArray<uint> indexDataU32 = CastIndices16To32(indexData);
                int vertexPositionOffset = data.GetVertexAttributeOffset(VertexAttribute.Position);
                AAAAMeshOptimizer.MeshletBuildResults meshletBuildResults = AAAAMeshOptimizer.BuildMeshlets(Allocator.Temp, vertexData,
                    (uint) vertexPositionOffset,
                    (uint) vertexBufferStride, indexDataU32,
                    new AAAAMeshOptimizer.MeshletGenerationParams
                    {
                        MaxVertices = 128,
                        MaxTriangles = 128,
                        ConeWeight = 0.5f,
                    }
                );
                
                meshletCollection.Meshlets = new Meshlet[meshletBuildResults.Meshlets.Length];
                
                for (int i = 0; i < meshletBuildResults.Meshlets.Length; i++)
                {
                    ref readonly meshopt_Meshlet meshoptMeshlet = ref meshletBuildResults.Meshlets.ElementAtRefReadonly(i);
                    meshopt_Bounds meshoptBounds =
                        AAAAMeshOptimizer.ComputeMeshletBounds(meshletBuildResults, i, vertexData, (uint) vertexPositionOffset, (uint) vertexBufferStride);
                    
                    meshletCollection.Meshlets[i] = new Meshlet
                    {
                        VertexOffset = (int) meshoptMeshlet.VertexOffset,
                        TriangleOffset = (int) meshoptMeshlet.TriangleOffset,
                        VertexCount = (int) meshoptMeshlet.VertexCount,
                        TriangleCount = (int) meshoptMeshlet.TriangleCount,
                        BoundingSphere = math.float4(meshoptBounds.Center, meshoptBounds.Radius),
                    };
                }
                
                const int floatSize = sizeof(float);
                int vertexBufferStrideInFloats = vertexBufferStride / floatSize;
                meshletCollection.VertexBuffer = new float[vertexBufferStrideInFloats * meshletBuildResults.Vertices.Length];
                
                fixed (float* pVertexBuffer = meshletCollection.VertexBuffer)
                {
                    for (int i = 0; i < meshletBuildResults.Vertices.Length; i++)
                    {
                        UnsafeUtility.MemCpy(
                            pVertexBuffer + vertexBufferStrideInFloats * i,
                            vertexData.ElementPtrReadonly((int) (vertexBufferStrideInFloats * meshletBuildResults.Vertices[i])),
                            vertexBufferStride
                        );
                    }
                }
                
                meshletCollection.IndexBuffer = new ushort[meshletBuildResults.Indices.Length];
                
                for (int i = 0; i < meshletBuildResults.Indices.Length; ++i)
                {
                    meshletCollection.IndexBuffer[i] = meshletBuildResults.Indices[i];
                }
                
                indexDataU32.Dispose();
                
                vertexData.Dispose();
                indexData.Dispose();
                
                meshletBuildResults.Dispose();
            }
            
            ctx.AddObjectToAsset(nameof(MeshletCollection), meshletCollection);
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
            string filename = "New Meshlet Collection." + Extension;
            
            Mesh mesh = Selection.objects.OfType<Mesh>().FirstOrDefault();
            if (mesh != null)
            {
                string path = AssetDatabase.GetAssetPath(mesh);
                string folder = File.Exists(path) ? Path.GetDirectoryName(path) : path;
                
                string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folder ?? "Assets", filename));
                
                File.WriteAllText(assetPath, string.Empty);
                AssetDatabase.Refresh();
                
                var assetImporter = (MeshletCollectionImporter) GetAtPath(assetPath);
                assetImporter.Mesh = mesh;
                Save(assetPath, assetImporter);
            }
        }
        
        private static async void Save(string assetPath, MeshletCollectionImporter importer)
        {
            EditorUtility.SetDirty(importer);
            AssetDatabase.SaveAssetIfDirty(importer);
            
            await Task.Yield();
            
            importer.SaveAndReimport();
            
            MeshletCollection meshletCollection = AssetDatabase.LoadAssetAtPath<MeshletCollection>(assetPath);
            Selection.activeObject = meshletCollection;
        }
    }
}