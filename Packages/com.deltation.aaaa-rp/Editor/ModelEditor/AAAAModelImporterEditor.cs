#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace DELTation.AAAARP.Editor.AssetPostProcessors
{
    [CustomEditor(typeof(ModelImporter))]
    [CanEditMultipleObjects]
    public sealed class AAAAModelImporterEditor : UnityEditor.Editor
    {
        private ModelImporterEditor _defaultEditor;

        public void OnEnable()
        {
            if (_defaultEditor == null)
            {
                _defaultEditor = (ModelImporterEditor) CreateEditor(targets, typeof(ModelImporterEditor));
                _defaultEditor.InternalSetAssetImporterTargetEditor(this);
            }
        }

        public void OnDisable()
        {
            if (_defaultEditor != null)
            {
                _defaultEditor.OnDisable();
            }
        }

        private void OnDestroy()
        {
            _defaultEditor.OnEnable();
            DestroyImmediate(_defaultEditor);
            _defaultEditor = null;
        }

        internal override void PostSerializedObjectCreation()
        {
            base.PostSerializedObjectCreation();
            _defaultEditor.PostSerializedObjectCreation();
        }

        public override GUIContent GetPreviewTitle() => _defaultEditor.activeTab is ModelImporterClipEditor activeTab
            ? new GUIContent(activeTab.selectedClipName)
            : base.GetPreviewTitle();

        public override bool HasPreviewGUI() => _defaultEditor.HasPreviewGUI();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawingInspectorGUI?.Invoke(this);

            _defaultEditor.OnInspectorGUI();
            serializedObject.ApplyModifiedProperties();
            
            foreach (Action action in _onAfterInspectorGUI)
            {
                action();
            }
            
            _onAfterInspectorGUI.Clear();
        }
        
        private readonly List<Action> _onAfterInspectorGUI = new();

        public void ScheduleAfterInspectorGUI(Action action)
        {
            _onAfterInspectorGUI.Add(action);
        }

        public static event Action<AAAAModelImporterEditor> DrawingInspectorGUI;
    }
}
#endif