using System.IO;
using System.Linq;
using DELTation.AAAARP.Materials;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;

namespace DELTation.AAAARP.Editor.AssetPostProcessors
{
    internal static class AAAAModelImporterUserDataDrawer
    {
        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            var wrapperPool = new ObjectPool<AAAAModelUserDataWrapper>(
                () =>
                {
                    AAAAModelUserDataWrapper wrapper = ScriptableObject.CreateInstance<AAAAModelUserDataWrapper>();
                    wrapper.hideFlags = HideFlags.DontSave;
                    return wrapper;
                },
                w => w.AAAASettings ??= new AAAAModelSettings(),
                actionOnDestroy: Object.DestroyImmediate
            );

            AAAAModelImporterEditor.DrawingInspectorGUI += editor =>
            {
                Object[] targets = editor.targets;
                var wrappers = new Object[targets.Length];

                for (int i = 0; i < targets.Length; i++)
                {
                    AAAAModelUserDataWrapper wrapper = wrapperPool.Get();
                    string userData = ((ModelImporter) targets[i]).userData;
                    AAAAModelSettings.Deserialize(userData, wrapper.AAAASettings);
                    wrappers[i] = wrapper;
                }

                var serializedObject = new SerializedObject(wrappers);
                serializedObject.Update();
                SerializedProperty property = serializedObject.FindProperty(nameof(AAAAModelUserDataWrapper.AAAASettings));
                EditorGUILayout.PropertyField(property);
                serializedObject.ApplyModifiedProperties();

                if (GUILayout.Button("Extract Materials"))
                {
                    string destinationFolder = Path.GetDirectoryName(((ModelImporter) targets[0]).assetPath);
                    destinationFolder = EditorUtility.OpenFolderPanel("Select Folder", destinationFolder, "");
                    if (destinationFolder.StartsWith(Application.dataPath) && Directory.Exists(destinationFolder))
                    {
                        destinationFolder = destinationFolder.Replace(Application.dataPath + "/", string.Empty);
                        destinationFolder = Path.Combine("Assets", destinationFolder);

                        for (int i = 0; i < wrappers.Length; i++)
                        {
                            var modelImporter = (ModelImporter) targets[i];
                            var wrapper = (AAAAModelUserDataWrapper) wrappers[i];

                            AAAAMaterialAsset[] materialAssets = AssetDatabase.LoadAllAssetsAtPath(modelImporter.assetPath)
                                .OfType<AAAAMaterialAsset>()
                                .ToArray();
                            foreach (AAAAMaterialAsset materialAsset in materialAssets)
                            {
                                AAAAMaterialAsset extractedMaterialAsset = Object.Instantiate(materialAsset);
                                extractedMaterialAsset.name = materialAsset.name;
                                string copyPath = Path.Combine(destinationFolder, materialAsset.name);
                                copyPath = Path.ChangeExtension(copyPath, ".asset");
                                copyPath = AssetDatabase.GenerateUniqueAssetPath(copyPath);
                                AssetDatabase.CreateAsset(extractedMaterialAsset, copyPath);

                                wrapper.AAAASettings.RemapMaterials.Add(new AAAAModelSettings.MaterialMapping
                                    {
                                        Name = materialAsset.name,
                                        MaterialAsset = extractedMaterialAsset,
                                    }
                                );
                            }
                        }

                        AssetDatabase.Refresh();
                    }
                }

                for (int i = 0; i < wrappers.Length; i++)
                {
                    var wrapper = (AAAAModelUserDataWrapper) wrappers[i];
                    var modelImporter = (ModelImporter) targets[i];
                    modelImporter.userData = AAAAModelSettings.Serialize(wrapper.AAAASettings);
                }

                serializedObject.Dispose();

                foreach (Object wrapper in wrappers)
                {
                    wrapperPool.Release((AAAAModelUserDataWrapper) wrapper);
                }

                EditorGUILayout.Space();
            };
        }
    }
}