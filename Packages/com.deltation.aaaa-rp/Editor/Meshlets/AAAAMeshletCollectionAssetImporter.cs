using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DELTation.AAAARP.Meshlets;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DELTation.AAAARP.Editor.Meshlets
{
    [ScriptedImporter(1, Extension)]
    internal class AAAAMeshletCollectionAssetImporter : ScriptedImporter
    {
        private const string Extension = "aaaameshletcollection";

        public Mesh Mesh;
        public bool OptimizeVertexCache;
        [Range(0.0f, 0.25f)]
        public float TargetError = 0.01f;
        [Range(0.0f, 0.25f)]
        public float TargetErrorSloppy = 0.001f;
        [Range(0.0f, 1.0f)]
        public float MinTriangleReductionPerStep = 0.8f;
        [Range(0, 10)]
        public int MaxMeshLODLevelCount;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            if (Mesh == null)
            {
                return;
            }

            AAAAMeshletCollectionAsset meshletCollection = ScriptableObject.CreateInstance<AAAAMeshletCollectionAsset>();
            meshletCollection.name = name;

            AAAAMeshletCollectionBuilder.Generate(meshletCollection, new AAAAMeshletCollectionBuilder.Parameters
                {
                    TargetErrorSloppy = TargetErrorSloppy,
                    MinTriangleReductionPerStep = MinTriangleReductionPerStep,
                    Mesh = Mesh,
                    TargetError = TargetError,
                    OptimizeVertexCache = OptimizeVertexCache,
                    MaxMeshLODLevelCount = MaxMeshLODLevelCount,
                    LogErrorHandler = e => ctx.LogImportError(e),
                }
            );

            var timer = new Stopwatch();
            timer.Start();

            ctx.AddObjectToAsset(nameof(AAAAMeshletCollectionAsset), meshletCollection);
            ctx.SetMainObject(meshletCollection);

            timer.Stop();
            Debug.Log($"Building meshlets for {ctx.assetPath} took {timer.ElapsedMilliseconds:F3} ms.", meshletCollection);
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