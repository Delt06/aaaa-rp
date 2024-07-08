using DELTation.AAAARP.MeshOptimizer.Runtime;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class SampleScript : MonoBehaviour
{
    public Mesh Mesh;
    
    private void Awake()
    {
        using (Mesh.MeshDataArray dataArray = Mesh.AcquireReadOnlyMeshData(Mesh))
        {
            Mesh.MeshData data = dataArray[0];
            int vertexBufferStride = data.GetVertexBufferStride(0);
            NativeArray<float> vertexData = data.GetVertexData<float>();
            NativeArray<ushort> indexData = data.GetIndexData<ushort>();
            
            NativeArray<uint> indexDataU32 = GetCastedIndices(indexData);
            int vertexPositionOffset = data.GetVertexAttributeOffset(VertexAttribute.Position);
            AAAAMeshOptimizer.MeshletBuildResults meshletBuildResults = AAAAMeshOptimizer.BuildMeshlets(Allocator.Temp, vertexData, (uint) vertexPositionOffset,
                (uint) vertexBufferStride, indexDataU32,
                new AAAAMeshOptimizer.MeshletGenerationParams
                {
                    MaxVertices = 128,
                    MaxTriangles = 128,
                    ConeWeight = 0.5f,
                }
            );
            
            for (int i = 0; i < meshletBuildResults.Meshlets.Length; i++)
            {
                meshopt_Bounds bounds =
                    AAAAMeshOptimizer.ComputeMeshletBounds(meshletBuildResults, i, vertexData, (uint) vertexPositionOffset, (uint) vertexBufferStride);
            }
            
            indexDataU32.Dispose();
            
            vertexData.Dispose();
            indexData.Dispose();
            
            meshletBuildResults.Dispose();
        }
    }
    
    private NativeArray<uint> GetCastedIndices(NativeArray<ushort> indices)
    {
        var result = new NativeArray<uint>(indices.Length, Allocator.Temp);
        for (int i = 0; i < indices.Length; i++)
        {
            result[i] = indices[i];
        }
        return result;
    }
}