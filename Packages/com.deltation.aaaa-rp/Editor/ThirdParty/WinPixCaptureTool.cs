using System;
using System.Collections;
using System.IO;
using DELTation.AAAARP.BindlessPlugin.Runtime;
using JetBrains.Annotations;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Editor.ThirdParty
{
    internal static class WinPixCaptureTool
    {
        private const string ToolPath = "Tools/AAAA RP/Take a PIX capture";

        [MenuItem(ToolPath)]
        private static void Capture() => EditorCoroutineUtility.StartCoroutine(Coroutine(), new object());

        [MenuItem(ToolPath, true)]
        private static bool CaptureValidate() => BindlessPluginBindings.IsPixLoaded() != 0 && IsSupportedWindow(EditorWindow.focusedWindow);

        private static IEnumerator Coroutine()
        {
            string directoryName = Path.Combine(Application.dataPath, "..", "PixCaptures");
            Directory.CreateDirectory(directoryName!);
            string filePath = Path.Combine(directoryName, "capture_" + DateTime.Now.ToString("yyyy-MM-dd--HH-mm-ss") + ".wpix");

            EditorWindow focusedWindow = EditorWindow.focusedWindow;
            if (IsSupportedWindow(focusedWindow))
            {
                focusedWindow.Show();
                focusedWindow.Focus();

                uint result = BindlessPluginBindings.BeginPixCapture(filePath);
                Assert.IsTrue(result == 0);

                if (focusedWindow is SceneView sceneView)
                {
                    sceneView.camera.Render();
                    sceneView.StartCoroutine(CaptureCoroutine(filePath));
                }
                else
                {
                    focusedWindow.Repaint();
                    yield return null;

                    focusedWindow.StartCoroutine(CaptureCoroutine(filePath));
                }
            }
            else
            {
                Debug.Log($"Unsupported focused window: {focusedWindow?.ToString() ?? "none"}.");
            }
        }

        private static bool IsSupportedWindow([CanBeNull] EditorWindow focusedWindow)
        {
            Type gameViewType = typeof(SceneView).Assembly.GetType("UnityEditor.GameView")!;
            return focusedWindow != null && (focusedWindow is SceneView || gameViewType.IsAssignableFrom(focusedWindow.GetType()));
        }

        private static IEnumerator CaptureCoroutine(string filePath)
        {
            GraphicsFence graphicsFence =
                Graphics.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.AllGPUOperations);

            while (!graphicsFence.passed)
            {
                yield return null;
            }

            uint result = BindlessPluginBindings.EndPixCapture();
            Assert.IsTrue(result == 0);

            Debug.Log("Successfully took a PIX capture: " + filePath);
            BindlessPluginBindings.OpenPixCapture(filePath);
        }
    }
}