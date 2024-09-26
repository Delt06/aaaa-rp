using System.Text;
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

    [GenerateHLSL]
    public enum AAAAGPUCullingDebugViewMode
    {
        None,
        Frustum,
        Occlusion,
        Cone,
    }

    public class AAAADebugDisplaySettingsRendering : IDebugDisplaySettingsData
    {
        private readonly AAAADebugStats _debugStats;

        public AAAADebugDisplaySettingsRendering(AAAADebugStats debugStats) => _debugStats = debugStats;

        public bool AutoUpdateRenderers { get; private set; }
        public AAAAVisibilityBufferDebugMode VisibilityBufferDebugMode { get; private set; }
        public bool ForceCullingFromMainCamera { get; private set; }
        public bool DebugGPUCulling { get; private set; }
        public AAAAGPUCullingDebugViewMode GPUCullingDebugViewMode { get; private set; }
        public int DebugGPUCullingViewInstanceCountLimit { get; private set; } = 32;
        public int DebugGPUCullingViewMeshletCountLimit { get; private set; } = 1024;

        public int ForcedMeshLODNodeDepth { get; private set; } = -1;
        public float MeshLODErrorThresholdBias { get; private set; }

        public AAAAGBufferDebugMode GBufferDebugMode { get; private set; }
        public Vector2 GBufferDebugDepthRemap { get; private set; } = new(0.1f, 50f);

        public bool AreAnySettingsActive => AutoUpdateRenderers ||
                                            GetOverridenVisibilityBufferDebugMode() != AAAAVisibilityBufferDebugMode.None ||
                                            ForceCullingFromMainCamera ||
                                            DebugGPUCulling ||
                                            GBufferDebugMode != AAAAGBufferDebugMode.None ||
                                            ForcedMeshLODNodeDepth >= 0 ||
                                            MeshLODErrorThresholdBias != 0.0f;

        public IDebugDisplaySettingsPanelDisposable CreatePanel() => new SettingsPanel(this, _debugStats);

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
            private readonly AAAADebugStats _stats;

            public SettingsPanel(AAAADebugDisplaySettingsRendering data, AAAADebugStats stats)
                : base(data)
            {
                AddWidget(Renderers.WidgetFactory.CreateFoldout(this));
                AddWidget(VisibilityBuffer.WidgetFactory.CreateFoldout(this));
                AddWidget(GBuffer.WidgetFactory.CreateFoldout(this));
                _stats = stats;
            }

            private static class Renderers
            {
                private static class Strings
                {
                    public static readonly DebugUI.Widget.NameAndTooltip AutoUpdateRenderers = new()
                        { name = "Auto Update Renderers" };
                }

                public static class WidgetFactory
                {
                    public static DebugUI.Widget CreateFoldout(SettingsPanel panel) =>
                        new DebugUI.Foldout
                        {
                            displayName = "Renderers",
                            flags = DebugUI.Flags.FrequentlyUsed,
                            isHeader = true,
                            opened = true,
                            children =
                            {
                                CreateAutoUpdateRenderers(panel),
                            },
                        };

                    private static DebugUI.Widget CreateAutoUpdateRenderers(SettingsPanel panel) => new DebugUI.BoolField
                    {
                        nameAndTooltip = Strings.AutoUpdateRenderers,
                        getter = () => panel.data.AutoUpdateRenderers,
                        setter = value => panel.data.AutoUpdateRenderers = value,
                    };
                }
            }

            private static class VisibilityBuffer
            {
                private static class Strings
                {
                    public static readonly DebugUI.Widget.NameAndTooltip DebugMode = new()
                        { name = "Debug Mode", tooltip = "The mode of visibility buffer debug display." };
                    public static readonly DebugUI.Widget.NameAndTooltip ForceCullingFromMainCamera = new()
                        { name = "Force Culling From Main Camera", tooltip = "Pass the main camera's data for GPU culling." };
                    public static readonly DebugUI.Widget.NameAndTooltip GPUCulling = new()
                        { name = "GPU Culling" };
                    public static readonly DebugUI.Widget.NameAndTooltip DebugGPUCulling = new()
                        { name = "Debug GPU Culling", tooltip = "Collect and show GPU culling statistics." };
                    public static readonly DebugUI.Widget.NameAndTooltip GPUCullingDebugViewMode = new()
                        { name = "Debug View Mode" };
                    public static readonly DebugUI.Widget.NameAndTooltip DebugGPUCullingViewInstanceCountLimit = new()
                        { name = "Culled Instance Count Limit", tooltip = "Remap displayed culled instance count." };
                    public static readonly DebugUI.Widget.NameAndTooltip DebugGPUCullingViewMeshletCountLimit = new()
                        { name = "Culled Meshlet Count Limit", tooltip = "Remap displayed culled meshlet count." };
                    public static readonly DebugUI.Widget.NameAndTooltip ForcedMeshLODNodeDepth = new()
                        { name = "Forced Mesh LOD Node Depth" };
                    public static readonly DebugUI.Widget.NameAndTooltip MeshLODErrorThresholdBias = new()
                        { name = "Mesh LOD Error Threshold Bias" };
                }

                public static class WidgetFactory
                {
                    private static readonly StringBuilder StringBuilder = new();

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
                                CreateForcedMeshLODNodeDepth(panel),
                                CreateMeshLODTargetErrorBias(panel),
                                CreateGPUCullingFoldout(panel),
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

                    private static DebugUI.Widget CreateGPUCullingFoldout(SettingsPanel panel) => new DebugUI.Foldout
                    {
                        nameAndTooltip = Strings.GPUCulling,
                        children =
                        {
                            new DebugUI.BoolField
                            {
                                nameAndTooltip = Strings.ForceCullingFromMainCamera,
                                getter = () => panel.data.ForceCullingFromMainCamera,
                                setter = value => panel.data.ForceCullingFromMainCamera = value,
                            },
                            new DebugUI.BoolField
                            {
                                nameAndTooltip = Strings.DebugGPUCulling,
                                getter = () => panel.data.DebugGPUCulling,
                                setter = value => panel.data.DebugGPUCulling = value,
                            },
                            new DebugUI.EnumField
                            {
                                nameAndTooltip = Strings.GPUCullingDebugViewMode,
                                autoEnum = typeof(AAAAGPUCullingDebugViewMode),
                                getter = () => (int) panel.data.GPUCullingDebugViewMode,
                                setter = value => panel.data.GPUCullingDebugViewMode = (AAAAGPUCullingDebugViewMode) value,
                                getIndex = () => (int) panel.data.GPUCullingDebugViewMode,
                                setIndex = value => panel.data.GPUCullingDebugViewMode = (AAAAGPUCullingDebugViewMode) value,
                            },
                            new DebugUI.IntField
                            {
                                nameAndTooltip = Strings.DebugGPUCullingViewInstanceCountLimit,
                                getter = () => panel.data.DebugGPUCullingViewInstanceCountLimit,
                                setter = value => panel.data.DebugGPUCullingViewInstanceCountLimit = value,
                                min = () => 1,
                                max = () => 128,
                                isHiddenCallback = () => !panel.data.DebugGPUCulling || panel.data.GPUCullingDebugViewMode == AAAAGPUCullingDebugViewMode.None,
                            },
                            new DebugUI.IntField
                            {
                                nameAndTooltip = Strings.DebugGPUCullingViewMeshletCountLimit,
                                getter = () => panel.data.DebugGPUCullingViewMeshletCountLimit,
                                setter = value => panel.data.DebugGPUCullingViewMeshletCountLimit = value,
                                min = () => 1,
                                max = () => 16384,
                                isHiddenCallback = () => !panel.data.DebugGPUCulling || panel.data.GPUCullingDebugViewMode == AAAAGPUCullingDebugViewMode.None,
                            },
                            new DebugUI.MessageBox
                            {
                                nameAndTooltip = Strings.DebugGPUCullingViewMeshletCountLimit,
                                messageCallback = () =>
                                {
                                    StringBuilder.Clear();
                                    panel._stats.BuildGPUCullingString(StringBuilder);
                                    return StringBuilder.ToString();
                                },
                                isHiddenCallback = () => !panel.data.DebugGPUCulling,
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

                    private static DebugUI.Widget CreateMeshLODTargetErrorBias(SettingsPanel panel) => new DebugUI.FloatField
                    {
                        nameAndTooltip = Strings.MeshLODErrorThresholdBias,
                        getter = () => panel.data.MeshLODErrorThresholdBias,
                        setter = value => panel.data.MeshLODErrorThresholdBias = value,
                        min = () => -1000.0f,
                        max = () => 1000.0f,
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