using DELTation.AAAARP.Meshlets;
using UnityEditor;
using UnityEngine.UIElements;

namespace DELTation.AAAARP.Editor.Meshlets
{
    [CustomEditor(typeof(AAAAMeshletCollectionAsset))]
    internal class AAAAMeshletCollectionAssetEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            var asset = (AAAAMeshletCollectionAsset) target;

            const bool isReadOnly = true;
            root.Add(new IntegerField("Top Mesh LOD Node Count")
                {
                    value = asset.TopMeshLODNodeCount,
                    isReadOnly = isReadOnly,
                }
            );
            root.Add(new IntegerField("Total Mesh LOD Nodes")
                {
                    value = asset.MeshLODNodes.Length,
                    isReadOnly = isReadOnly,
                }
            );
            root.Add(new IntegerField("Mesh LOD Level Count")
                {
                    value = asset.MeshLODLevelCount,
                    isReadOnly = isReadOnly,
                }
            );
            root.Add(new IntegerField("Total Meshlets")
                {
                    value = asset.Meshlets.Length,
                    isReadOnly = isReadOnly,
                }
            );
            root.Add(new IntegerField("Total Vertices")
                {
                    value = asset.VertexBuffer.Length,
                    isReadOnly = isReadOnly,
                }
            );
            root.Add(new IntegerField("Total Indices")
                {
                    value = asset.IndexBuffer.Length,
                    isReadOnly = isReadOnly,
                }
            );

            return root;
        }
    }
}