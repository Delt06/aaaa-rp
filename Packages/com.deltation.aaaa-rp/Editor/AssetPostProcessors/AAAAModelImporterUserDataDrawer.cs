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
                    JsonUtility.FromJsonOverwrite(userData, wrapper.AAAASettings);
                    wrappers[i] = wrapper;
                }

                var serializedObject = new SerializedObject(wrappers);
                serializedObject.Update();
                SerializedProperty property = serializedObject.FindProperty(nameof(AAAAModelUserDataWrapper.AAAASettings));
                EditorGUILayout.PropertyField(property);
                serializedObject.ApplyModifiedProperties();

                for (int i = 0; i < wrappers.Length; i++)
                {
                    var wrapper = (AAAAModelUserDataWrapper) wrappers[i];
                    var modelImporter = (ModelImporter) targets[i];
                    modelImporter.userData = JsonUtility.ToJson(wrapper.AAAASettings);
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