using UnityEngine.Rendering;

namespace DELTation.AAAARP.Debugging
{
    [GenerateHLSL]
    public enum AAAAVisibilityBufferDebugMode
    {
        None,
        BarycentricCoordinates,
        InstanceID,
        MeshletID,
        IndexID,
    }

    public class AAAADebugDisplaySettingsRendering : IDebugDisplaySettingsData
    {
        public AAAAVisibilityBufferDebugMode VisibilityBufferDebugMode { get; set; }

        public bool AreAnySettingsActive => VisibilityBufferDebugMode != AAAAVisibilityBufferDebugMode.None;
        public IDebugDisplaySettingsPanelDisposable CreatePanel() => new SettingsPanel(this);

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
                        children =
                        {
                            WidgetFactory.CreateVisibilityBufferDebugMode(this),
                        },
                    }
                );
            }

            private static class Strings
            {
                public static readonly DebugUI.Widget.NameAndTooltip VisibilityBufferDebugMode = new()
                    { name = "Visibility Buffer Debug Mode", tooltip = "The mode of visibility buffer debug display." };
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
            }
        }
    }
}