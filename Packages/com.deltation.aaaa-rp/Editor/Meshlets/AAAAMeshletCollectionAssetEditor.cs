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
            root.Add(new IntegerField("Top Meshlet Count")
                {
                    value = asset.TopMeshletCount,
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