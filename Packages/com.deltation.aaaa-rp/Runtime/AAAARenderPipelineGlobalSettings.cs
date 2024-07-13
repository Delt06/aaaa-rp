using System.Collections.Generic;
using System.ComponentModel;
using DELTation.AAAARP.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP
{
    [DisplayInfo(name = "AAAARP Global Settings Asset", order = CoreUtils.Sections.section4 + 2)]
    [SupportedOnRenderPipeline(typeof(AAAARenderPipelineAsset))]
    [DisplayName("AAAARP")]
    internal class AAAARenderPipelineGlobalSettings : RenderPipelineGlobalSettings<AAAARenderPipelineGlobalSettings, AAAARenderPipeline>
    {

        public const string DefaultAssetName = "AAAARenderPipelineGlobalSettings";
        [SerializeField] private RenderPipelineGraphicsSettingsContainer _settings = new();

        protected override List<IRenderPipelineGraphicsSettings> settingsList => _settings.settingsList;

        #if UNITY_EDITOR
        internal static string DefaultPath => $"Assets/{DefaultAssetName}.asset";

        //Making sure there is at least one AAAARenderPipelineGlobalSettings instance in the project
        internal static AAAARenderPipelineGlobalSettings Ensure(bool canCreateNewAsset = true)
        {
            var currentInstance = GraphicsSettings.GetSettingsForRenderPipeline<AAAARenderPipeline>() as AAAARenderPipelineGlobalSettings;

            if (RenderPipelineGlobalSettingsUtils.TryEnsure<AAAARenderPipelineGlobalSettings, AAAARenderPipeline>(ref currentInstance, DefaultPath,
                    canCreateNewAsset
                ))
            {
                return currentInstance;
            }

            return null;
        }

        public override void Initialize(RenderPipelineGlobalSettings source = null) { }

#endif // #if UNITY_EDITOR
    }
}