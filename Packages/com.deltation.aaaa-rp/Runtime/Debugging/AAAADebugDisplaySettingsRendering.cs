using System.Text;
using DELTation.AAAARP.Lighting;
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
    public enum AAAAGPUCullingDebugViewMode
    {
        None,
        Frustum,
        Occlusion,
        Cone,
    }

    [GenerateHLSL]
    public enum AAAAGBufferDebugMode
    {
        None,
        Depth,
        Albedo,
        Normals,
        Masks,
    }

    [GenerateHLSL]
    public enum AAAALightingDebugMode
    {
        None,
        ClusterZ,
        DeferredLights,
        DirectionalLightCascades,
        AmbientOcclusion,
        BentNormals,
        IndirectDiffuse,
        IndirectSpecular,
    }

    [GenerateHLSL]
    public enum AAAALightPropagationVolumesDebugMode
    {
        Radiance,
        BlockingPotential,
        SkyOcclusion,
    }

    [GenerateHLSL]
    public enum AAAAVxgiDebugMode
    {
        Radiance,
        Normals,
    }

    public class AAAADebugDisplaySettingsRendering : IDebugDisplaySettingsData
    {
        private readonly AAAADebugStats _debugStats;

        public AAAADebugDisplaySettingsRendering(AAAADebugStats debugStats) => _debugStats = debugStats;

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

        public AAAALightingDebugMode LightingDebugMode { get; private set; }
        public Vector2 LightingDebugCountRemap { get; private set; } = new(1, 128);
        public int LightingDebugLightIndex { get; private set; }

        public bool LightPropagationVolumesDebug { get; private set; }
        public AAAALightPropagationVolumesDebugMode LightPropagationVolumesDebugMode { get; private set; }
        public float LightPropagationVolumesDebugSize { get; private set; } = 0.1f;
        public float LightPropagationVolumesDebugIntensity { get; private set; } = 1.0f;
        public float LightPropagationVolumesDebugClipDistance { get; private set; } = 2.0f;
        public bool VXGIDebug { get; private set; }
        public bool VXGIDebugOverlay { get; private set; } = true;
        public AAAAVxgiDebugMode VXGIDebugMode { get; private set; } = AAAAVxgiDebugMode.Radiance;
        public int VXGIMipLevel { get; private set; }

        public bool AreAnySettingsActive => GetOverridenVisibilityBufferDebugMode() != AAAAVisibilityBufferDebugMode.None ||
                                            ForceCullingFromMainCamera ||
                                            DebugGPUCulling ||
                                            GBufferDebugMode != AAAAGBufferDebugMode.None ||
                                            ForcedMeshLODNodeDepth >= 0 ||
                                            MeshLODErrorThresholdBias != 0.0f ||
                                            LightingDebugMode != AAAALightingDebugMode.None;

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
                AddWidget(VisibilityBuffer.WidgetFactory.CreateFoldout(this));
                AddWidget(GBuffer.WidgetFactory.CreateFoldout(this));
                AddWidget(Lighting.WidgetFactory.CreateFoldout(this));
                _stats = stats;
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
                                isHiddenCallback = () => !panel.data.DebugGPUCulling,
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

            private static class Lighting
            {
                private static class Strings
                {
                    public static readonly DebugUI.Widget.NameAndTooltip DebugMode = new()
                        { name = "Debug Mode", tooltip = "The mode of lighting debug display." };
                    public static readonly DebugUI.Widget.NameAndTooltip LightCountRemap = new() { name = "Light Count Remap" };
                    public static readonly DebugUI.Widget.NameAndTooltip LightIndex = new() { name = "Light Index" };
                }

                public static class WidgetFactory
                {
                    public static DebugUI.Widget CreateFoldout(SettingsPanel panel) =>
                        new DebugUI.Foldout
                        {
                            displayName = "Lighting",
                            flags = DebugUI.Flags.FrequentlyUsed,
                            isHeader = true,
                            opened = true,
                            children =
                            {
                                CreateLightingDebugMode(panel),
                                CreateLightCountRemap(panel),
                                CreateLightIndex(panel),
                                LightPropagationVolumes.WidgetFactory.CreateFoldout(panel),
                                VXGI.WidgetFactory.CreateFoldout(panel),
                            },
                        };

                    private static DebugUI.Widget CreateLightingDebugMode(SettingsPanel panel) => new DebugUI.EnumField
                    {
                        nameAndTooltip = Strings.DebugMode,
                        autoEnum = typeof(AAAALightingDebugMode),
                        getter = () => (int) panel.data.LightingDebugMode,
                        setter = value => panel.data.LightingDebugMode = (AAAALightingDebugMode) value,
                        getIndex = () => (int) panel.data.LightingDebugMode,
                        setIndex = value => panel.data.LightingDebugMode = (AAAALightingDebugMode) value,
                    };

                    private static DebugUI.Widget CreateLightCountRemap(SettingsPanel panel) => new DebugUI.Vector2Field
                    {
                        nameAndTooltip = Strings.LightCountRemap,
                        getter = () => panel.data.LightingDebugCountRemap,
                        setter = value => panel.data.LightingDebugCountRemap = value,
                        isHiddenCallback = () => !(panel.data.LightingDebugMode is AAAALightingDebugMode.ClusterZ or AAAALightingDebugMode.DeferredLights),
                        onValueChanged = (field, value) =>
                        {
                            Vector2 newValue = math.max(value, 0);
                            if (value != newValue)
                            {
                                field.SetValue(newValue);
                            }
                        },
                    };

                    private static DebugUI.Widget CreateLightIndex(SettingsPanel panel) => new DebugUI.IntField
                    {
                        nameAndTooltip = Strings.LightIndex,
                        getter = () => panel.data.LightingDebugLightIndex,
                        setter = value => panel.data.LightingDebugLightIndex = value,
                        isHiddenCallback = () => panel.data.LightingDebugMode is not AAAALightingDebugMode.DirectionalLightCascades,
                        min = () => 0,
                        max = () => AAAALightingConstantBuffer.MaxDirectionalLights - 1,
                    };
                }
            }

            private static class VXGI
            {
                private static class Strings
                {
                    public static readonly DebugUI.Widget.NameAndTooltip Debug = new() { name = "Debug" };
                    public static readonly DebugUI.Widget.NameAndTooltip DebugMode = new() { name = "Debug Mode" };
                    public static readonly DebugUI.Widget.NameAndTooltip MipLevel = new() { name = "Mip Level" };
                    public static readonly DebugUI.Widget.NameAndTooltip Overlay = new() { name = "Overlay" };
                }

                public static class WidgetFactory
                {
                    public static DebugUI.Widget CreateFoldout(SettingsPanel panel) =>
                        new DebugUI.Foldout
                        {
                            displayName = "Voxel GI",
                            flags = DebugUI.Flags.FrequentlyUsed,
                            isHeader = true,
                            opened = true,
                            children =
                            {
                                CreateDebug(panel),
                                CreateDebugMode(panel),
                                CreateDebugMipLevel(panel),
                                CreateDebugOverlay(panel),
                            },
                        };

                    private static DebugUI.Widget CreateDebug(SettingsPanel panel) => new DebugUI.BoolField
                    {
                        nameAndTooltip = Strings.Debug,
                        getter = () => panel.data.VXGIDebug,
                        setter = value => panel.data.VXGIDebug = value,
                    };

                    private static DebugUI.Widget CreateDebugMode(SettingsPanel panel) => new DebugUI.EnumField
                    {
                        nameAndTooltip = Strings.DebugMode,
                        isHiddenCallback = () => !panel.data.VXGIDebug,
                        autoEnum = typeof(AAAAVxgiDebugMode),
                        getter = () => (int) panel.data.VXGIDebugMode,
                        setter = value => panel.data.VXGIDebugMode = (AAAAVxgiDebugMode) value,
                        getIndex = () => (int) panel.data.VXGIDebugMode,
                        setIndex = value => panel.data.VXGIDebugMode = (AAAAVxgiDebugMode) value,
                    };

                    private static DebugUI.Widget CreateDebugMipLevel(SettingsPanel panel) => new DebugUI.IntField
                    {
                        nameAndTooltip = Strings.MipLevel,
                        getter = () => panel.data.VXGIMipLevel,
                        setter = value => panel.data.VXGIMipLevel = value,
                        min = () => 0,
                        max = () => 10,
                    };

                    private static DebugUI.Widget CreateDebugOverlay(SettingsPanel panel) => new DebugUI.BoolField
                    {
                        nameAndTooltip = Strings.Overlay,
                        isHiddenCallback = () => !panel.data.VXGIDebug,
                        getter = () => panel.data.VXGIDebugOverlay,
                        setter = value => panel.data.VXGIDebugOverlay = value,
                    };
                }
            }

            private static class LightPropagationVolumes
            {
                private static class Strings
                {
                    public static readonly DebugUI.Widget.NameAndTooltip Debug = new() { name = "Debug" };
                    public static readonly DebugUI.Widget.NameAndTooltip DebugMode = new() { name = "Debug Mode" };
                    public static readonly DebugUI.Widget.NameAndTooltip DebugSize = new() { name = "Debug Size" };
                    public static readonly DebugUI.Widget.NameAndTooltip DebugIntensity = new() { name = "Debug Intensity" };
                    public static readonly DebugUI.Widget.NameAndTooltip DebugClipDistance = new() { name = "Debug Clip Distance" };
                }

                public static class WidgetFactory
                {
                    public static DebugUI.Widget CreateFoldout(SettingsPanel panel) =>
                        new DebugUI.Foldout
                        {
                            displayName = "Light Propagation Volumes",
                            flags = DebugUI.Flags.FrequentlyUsed,
                            isHeader = true,
                            opened = true,
                            children =
                            {
                                CreateDebug(panel),
                                CreateDebugMode(panel),
                                CreateDebugSize(panel),
                                CreateDebugIntensity(panel),
                                CreateDebugClipDistance(panel),
                            },
                        };

                    private static DebugUI.Widget CreateDebug(SettingsPanel panel) => new DebugUI.BoolField
                    {
                        nameAndTooltip = Strings.Debug,
                        getter = () => panel.data.LightPropagationVolumesDebug,
                        setter = value => panel.data.LightPropagationVolumesDebug = value,
                    };

                    private static DebugUI.Widget CreateDebugMode(SettingsPanel panel) => new DebugUI.EnumField
                    {
                        nameAndTooltip = Strings.DebugMode,
                        isHiddenCallback = () => !panel.data.LightPropagationVolumesDebug,
                        autoEnum = typeof(AAAALightPropagationVolumesDebugMode),
                        getter = () => (int) panel.data.LightPropagationVolumesDebugMode,
                        setter = value => panel.data.LightPropagationVolumesDebugMode = (AAAALightPropagationVolumesDebugMode) value,
                        getIndex = () => (int) panel.data.LightPropagationVolumesDebugMode,
                        setIndex = value => panel.data.LightPropagationVolumesDebugMode = (AAAALightPropagationVolumesDebugMode) value,
                    };

                    private static DebugUI.Widget CreateDebugSize(SettingsPanel panel) => new DebugUI.FloatField
                    {
                        nameAndTooltip = Strings.DebugSize,
                        isHiddenCallback = () => !panel.data.LightPropagationVolumesDebug,
                        getter = () => panel.data.LightPropagationVolumesDebugSize,
                        setter = value => panel.data.LightPropagationVolumesDebugSize = value,
                        min = () => 0.0f,
                        max = () => 1.0f,
                    };

                    private static DebugUI.Widget CreateDebugIntensity(SettingsPanel panel) => new DebugUI.FloatField
                    {
                        nameAndTooltip = Strings.DebugIntensity,
                        isHiddenCallback = () => !panel.data.LightPropagationVolumesDebug,
                        getter = () => panel.data.LightPropagationVolumesDebugIntensity,
                        setter = value => panel.data.LightPropagationVolumesDebugIntensity = value,
                        min = () => 0.0f,
                        max = () => 10.0f,
                    };

                    private static DebugUI.Widget CreateDebugClipDistance(SettingsPanel panel) => new DebugUI.FloatField
                    {
                        nameAndTooltip = Strings.DebugClipDistance,
                        isHiddenCallback = () => !panel.data.LightPropagationVolumesDebug,
                        getter = () => panel.data.LightPropagationVolumesDebugClipDistance,
                        setter = value => panel.data.LightPropagationVolumesDebugClipDistance = value,
                        min = () => 0.0f,
                        max = () => 10.0f,
                    };
                }
            }
        }
    }
}