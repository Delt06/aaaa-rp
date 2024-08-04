using DELTation.AAAARP.Data;
using Unity.Mathematics;
using UnityEngine;
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
        VertexID,
    }

    [GenerateHLSL]
    public enum AAAAGBufferDebugMode
    {
        None,
        Depth,
        Albedo,
        Normals,
    }

    public class AAAADebugDisplaySettingsRendering : IDebugDisplaySettingsData
    {
        public AAAAVisibilityBufferDebugMode VisibilityBufferDebugMode { get; private set; }
        public bool ForceCullingFromMainCamera { get; private set; }

        public bool OverrideFullScreenTriangleBudget { get; private set; }
        public int FullScreenTriangleBudget { get; private set; } = AAAAMeshLODSettings.DefaultFullScreenTriangleBudget;
        public int ForcedMeshLODNodeDepth { get; private set; } = -1;

        public AAAAGBufferDebugMode GBufferDebugMode { get; private set; }
        public Vector2 GBufferDebugDepthRemap { get; private set; } = new(0.1f, 50f);

        public bool AreAnySettingsActive => GetOverridenVisibilityBufferDebugMode() != AAAAVisibilityBufferDebugMode.None ||
                                            ForceCullingFromMainCamera ||
                                            OverrideFullScreenTriangleBudget ||
                                            GBufferDebugMode != AAAAGBufferDebugMode.None ||
                                            ForcedMeshLODNodeDepth >= 0;

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
                AddWidget(VisibilityBuffer.WidgetFactory.CreateFoldout(this));
                AddWidget(GBuffer.WidgetFactory.CreateFoldout(this));
            }

            private static class VisibilityBuffer
            {
                private static class Strings
                {
                    public static readonly DebugUI.Widget.NameAndTooltip DebugMode = new()
                        { name = "Debug Mode", tooltip = "The mode of visibility buffer debug display." };
                    public static readonly DebugUI.Widget.NameAndTooltip ForceCullingFromMainCamera = new()
                        { name = "Force Culling From Main Camera", tooltip = "Pass the main camera's data for GPU culling." };
                    public static readonly DebugUI.Widget.NameAndTooltip OverrideFullScreenTriangleBudget = new()
                        { name = "Override Full Screen Triangle Budget" };
                    public static readonly DebugUI.Widget.NameAndTooltip FullScreenTriangleBudget = new()
                        { name = "Budget" };
                    public static readonly DebugUI.Widget.NameAndTooltip ForcedMeshLODNodeDepth = new()
                        { name = "Forced Mesh LOD Node Depth" };
                }

                public static class WidgetFactory
                {
                    public static DebugUI.Widget CreateFoldout(SettingsPanel panel) =>
                        new DebugUI.Foldout
                        {
                            displayName = "Visibility Buffer",
                            flags = DebugUI.Flags.FrequentlyUsed,
                            isHeader = true,
                            opened = true,
                            children =
                            {
                                CreateVisibilityBufferDebugMode(panel),
                                CreateForceCullingFrustumOfMainCamera(panel),
                                CreateOverrideFullScreenTriangleBudget(panel),
                                CreateFullScreenTriangleBudget(panel),
                                CreateForcedMeshLODNodeDepth(panel),
                            },
                        };

                    private static DebugUI.Widget CreateVisibilityBufferDebugMode(SettingsPanel panel) => new DebugUI.EnumField
                    {
                        nameAndTooltip = Strings.DebugMode,
                        autoEnum = typeof(AAAAVisibilityBufferDebugMode),
                        getter = () => (int) panel.data.VisibilityBufferDebugMode,
                        setter = value => panel.data.VisibilityBufferDebugMode = (AAAAVisibilityBufferDebugMode) value,
                        getIndex = () => (int) panel.data.VisibilityBufferDebugMode,
                        setIndex = value => panel.data.VisibilityBufferDebugMode = (AAAAVisibilityBufferDebugMode) value,
                    };

                    private static DebugUI.Widget CreateForceCullingFrustumOfMainCamera(SettingsPanel panel) => new DebugUI.BoolField
                    {
                        nameAndTooltip = Strings.ForceCullingFromMainCamera,
                        getter = () => panel.data.ForceCullingFromMainCamera,
                        setter = value => panel.data.ForceCullingFromMainCamera = value,
                    };

                    private static DebugUI.Widget CreateOverrideFullScreenTriangleBudget(SettingsPanel panel) => new DebugUI.BoolField
                    {
                        nameAndTooltip = Strings.OverrideFullScreenTriangleBudget,
                        getter = () => panel.data.OverrideFullScreenTriangleBudget,
                        setter = value => panel.data.OverrideFullScreenTriangleBudget = value,
                    };

                    private static DebugUI.Widget CreateFullScreenTriangleBudget(SettingsPanel panel) => new DebugUI.Container
                    {
                        children =
                        {
                            new DebugUI.IntField
                            {
                                nameAndTooltip = Strings.FullScreenTriangleBudget,
                                getter = () => panel.data.FullScreenTriangleBudget,
                                setter = value => panel.data.FullScreenTriangleBudget = value,
                                min = () => 0,
                                isHiddenCallback = () => !panel.data.OverrideFullScreenTriangleBudget,
                            },
                        },
                    };

                    private static DebugUI.Widget CreateForcedMeshLODNodeDepth(SettingsPanel panel) => new DebugUI.IntField
                    {
                        nameAndTooltip = Strings.ForcedMeshLODNodeDepth,
                        getter = () => panel.data.ForcedMeshLODNodeDepth,
                        setter = value => panel.data.ForcedMeshLODNodeDepth = value,
                        min = () => -1,
                        max = () => 32,
                    };
                }
            }

            private static class GBuffer
            {
                private static class Strings
                {
                    public static readonly DebugUI.Widget.NameAndTooltip DebugMode = new()
                        { name = "Debug Mode", tooltip = "The mode of GBuffer debug display." };
                    public static readonly DebugUI.Widget.NameAndTooltip DepthRemap = new()
                        { name = "Depth Remap", tooltip = "Range of displayed depth buffer values." };
                }

                public static class WidgetFactory
                {
                    public static DebugUI.Widget CreateFoldout(SettingsPanel panel) =>
                        new DebugUI.Foldout
                        {
                            displayName = "GBuffer",
                            flags = DebugUI.Flags.FrequentlyUsed,
                            isHeader = true,
                            opened = true,
                            children =
                            {
                                CreateGBufferDebugMode(panel),
                                CreateDepthRemap(panel),
                            },
                        };

                    private static DebugUI.Widget CreateGBufferDebugMode(SettingsPanel panel) => new DebugUI.EnumField
                    {
                        nameAndTooltip = Strings.DebugMode,
                        autoEnum = typeof(AAAAGBufferDebugMode),
                        getter = () => (int) panel.data.GBufferDebugMode,
                        setter = value => panel.data.GBufferDebugMode = (AAAAGBufferDebugMode) value,
                        getIndex = () => (int) panel.data.GBufferDebugMode,
                        setIndex = value => panel.data.GBufferDebugMode = (AAAAGBufferDebugMode) value,
                    };

                    private static DebugUI.Widget CreateDepthRemap(SettingsPanel panel) => new DebugUI.Vector2Field
                    {
                        nameAndTooltip = Strings.DepthRemap,
                        getter = () => panel.data.GBufferDebugDepthRemap,
                        setter = value => panel.data.GBufferDebugDepthRemap = value,
                        isHiddenCallback = () => panel.data.GBufferDebugMode != AAAAGBufferDebugMode.Depth,
                        onValueChanged = (field, value) =>
                        {
                            Vector2 newValue = math.max(value, 0);
                            if (value != newValue)
                            {
                                field.SetValue(newValue);
                            }
                        },
                    };
                }
            }
        }
    }
}