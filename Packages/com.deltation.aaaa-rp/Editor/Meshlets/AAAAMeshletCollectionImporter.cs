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
    internal class AAAAMeshletCollectionImporter : ScriptedImporter
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
            if (Mesh.indexFormat != IndexFormat.UInt16)
            {
                ctx.LogImportError("Only UInt16 index format is supported.", this);
                return;
            }
            
            AAAAMeshletCollection meshletCollection = ScriptableObject.CreateInstance<AAAAMeshletCollection>();
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
                    AAAAMeshletCollection.MeshletGenerationParams
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
                        BoundingSphere = math.float4(meshoptBounds.Center, meshoptBounds.Radius),
                    };
                }
                
                meshletCollection.VertexBuffer = new AAAAMeshletVertex[meshletBuildResults.Vertices.Length];
                
                byte* pVertices = (byte*) vertexData.GetUnsafeReadOnlyPtr();
                int vertexNormalOffset = data.GetVertexAttributeOffset(VertexAttribute.Normal);
                int vertexTangentOffset = data.GetVertexAttributeOffset(VertexAttribute.Tangent);
                int vertexUVOffset = data.GetVertexAttributeOffset(VertexAttribute.TexCoord0);
                
                for (int i = 0; i < meshletBuildResults.Vertices.Length; i++)
                {
                    byte* pVertex = pVertices + vertexBufferStride * meshletBuildResults.Vertices[i];
                    
                    meshletCollection.VertexBuffer[i] = new AAAAMeshletVertex
                    {
                        Position = math.float4(*(float3*) (pVertex + vertexPositionOffset), 1),
                        Normal = math.float4(*(float3*) (pVertex + vertexNormalOffset), 1),
                        Tangent = *(float4*) (pVertex + vertexTangentOffset),
                        UV = *(float2*) (pVertex + vertexUVOffset),
                    };
                }
                
                meshletCollection.IndexBuffer = new byte[meshletBuildResults.Indices.Length];
                
                for (int i = 0; i < meshletBuildResults.Indices.Length; ++i)
                {
                    meshletCollection.IndexBuffer[i] = meshletBuildResults.Indices[i];
                }
                
                indexDataU32.Dispose();
                
                vertexData.Dispose();
                indexData.Dispose();
                
                meshletBuildResults.Dispose();
            }
            
            ctx.AddObjectToAsset(nameof(AAAAMeshletCollection), meshletCollection);
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
                
                var assetImporter = (AAAAMeshletCollectionImporter) GetAtPath(assetPath);
                assetImporter.Mesh = mesh;
                Save(assetPath, assetImporter);
            }
            else
            {
                ProjectWindowUtil.CreateAssetWithContent("New Meshlet Collection." + Extension, string.Empty);
            }
        }
        
        private static async void Save(string assetPath, AAAAMeshletCollectionImporter importer)
        {
            EditorUtility.SetDirty(importer);
            AssetDatabase.SaveAssetIfDirty(importer);
            
            await Task.Yield();
            
            importer.SaveAndReimport();
            
            AAAAMeshletCollection meshletCollection = AssetDatabase.LoadAssetAtPath<AAAAMeshletCollection>(assetPath);
            Selection.activeObject = meshletCollection;
        }
    }
}