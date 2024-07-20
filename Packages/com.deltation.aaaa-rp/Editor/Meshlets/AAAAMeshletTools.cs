using System.Diagnostics;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace DELTation.AAAARP.Editor.Meshlets
{
    internal static class MeshletTools
    {
        [MenuItem("Tools/AAAA RP/Reimport Meshlets")]
        public static void ReimportMeshlets()
        {
            try
            {
                int count = 0;

                var stopwatch = Stopwatch.StartNew();

                string[] guids = AssetDatabase.FindAssets("t: AAAAMeshletCollectionAsset");
                for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
                {
                    string guid = guids[guidIndex];
                    string path = AssetDatabase.GUIDToAssetPath(guid);

                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        EditorUtility.DisplayProgressBar("Reimporting meshlets", path, (float) guidIndex / guids.Length);

                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                        ++count;
                    }
                }

                stopwatch.Stop();
                Debug.Log($"Reimported {count} meshlet collections, which took {stopwatch.Elapsed.TotalSeconds:F3}s.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}