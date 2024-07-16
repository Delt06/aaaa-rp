using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DELTation.AAAARP.Debugging
{
    [GenerateHLSL]
    public enum AAAAVisibilityBufferDebugMode
    {
        None,
        Wireframe,
        BarycentricCoordinates,
        InstanceID,
        MeshletID,
        IndexID,
    }

    public class AAAADebugDisplaySettingsRendering : IDebugDisplaySettingsData
    {
        public AAAAVisibilityBufferDebugMode VisibilityBufferDebugMode { get; private set; }
        public bool ForceCullingFromMainCamera { get; private set; }

        public float MeshLODBias { get; private set; }

        public bool AreAnySettingsActive => GetOverridenVisibilityBufferDebugMode() != AAAAVisibilityBufferDebugMode.None ||
                                            ForceCullingFromMainCamera ||
                                            MeshLODBias != 0;

        public IDebugDisplaySettingsPanelDisposable CreatePanel() => new SettingsPanel(this);

        public AAAAVisibilityBufferDebugMode GetOverridenVisibilityBufferDebugMode()
        {
#if UNITY_EDITOR
            DrawCameraMode? cameraMode = SceneView.currentDrawingSceneView?.cameraMode.drawMode;
            if (cameraMode == DrawCameraMode.TexturedWire)
            {
                return AAAAVisibilityBufferDebugMode.Wireframe;
            }
#endif
            return VisibilityBufferDebugMode;
        }

        [DisplayInfo(name = "Rendering", order = 1)]
        private class SettingsPanel : DebugDisplaySettingsPanel<AAAADebugDisplaySettingsRendering>
        {
            public SettingsPanel(AAAADebugDisplaySettingsRendering data)
                : base(data)
            {
                AddWidget(new DebugUI.Foldout
                    {
                        displayName = "Rendering Debug",
                        flags = DebugUI.Flags.FrequentlyUsed,
                        isHeader = true,
                        opened = true,
                        children =
                        {
                            WidgetFactory.CreateVisibilityBufferDebugMode(this),
                            WidgetFactory.CreateForceCullingFrustumOfMainCamera(this),
                            WidgetFactory.CreateMeshLODBias(this),
                        },
                    }
                );
            }

            private static class Strings
            {
                public static readonly DebugUI.Widget.NameAndTooltip VisibilityBufferDebugMode = new()
                    { name = "Visibility Buffer Debug Mode", tooltip = "The mode of visibility buffer debug display." };
                public static readonly DebugUI.Widget.NameAndTooltip ForceCullingFromMainCamera = new()
                    { name = "Force Culling From Main Camera", tooltip = "Pass the main camera's data for GPU culling." };
                public static readonly DebugUI.Widget.NameAndTooltip MeshLODBias = new()
                    { name = "Mesh LOD Bias", tooltip = "Extra bias for mesh LOD selection." };
            }

            private static class WidgetFactory
            {
                internal static DebugUI.Widget CreateVisibilityBufferDebugMode(SettingsPanel panel) => new DebugUI.EnumField
                {
                    nameAndTooltip = Strings.VisibilityBufferDebugMode,
                    autoEnum = typeof(AAAAVisibilityBufferDebugMode),
                    getter = () => (int) panel.data.VisibilityBufferDebugMode,
                    setter = value => panel.data.VisibilityBufferDebugMode = (AAAAVisibilityBufferDebugMode) value,
                    getIndex = () => (int) panel.data.VisibilityBufferDebugMode,
                    setIndex = value => panel.data.VisibilityBufferDebugMode = (AAAAVisibilityBufferDebugMode) value,
                };

                internal static DebugUI.Widget CreateForceCullingFrustumOfMainCamera(SettingsPanel panel) => new DebugUI.BoolField
                {
                    nameAndTooltip = Strings.ForceCullingFromMainCamera,
                    getter = () => panel.data.ForceCullingFromMainCamera,
                    setter = value => panel.data.ForceCullingFromMainCamera = value,
                };

                internal static DebugUI.Widget CreateMeshLODBias(SettingsPanel panel) => new DebugUI.FloatField
                {
                    nameAndTooltip = Strings.MeshLODBias,
                    getter = () => panel.data.MeshLODBias,
                    setter = value => panel.data.MeshLODBias = value,
                    min = () => -(float) AAAAMeshletConfiguration.LodCount,
                    max = () => (float) AAAAMeshletConfiguration.LodCount - 1,
                };
            }
        }
    }
}