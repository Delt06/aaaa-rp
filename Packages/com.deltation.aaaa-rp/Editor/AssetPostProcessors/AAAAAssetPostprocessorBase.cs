using DELTation.AAAARP.Data;
using UnityEditor;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Editor.AssetPostProcessors
{
    internal abstract class AAAAAssetPostprocessorBase : AssetPostprocessor
    {
        protected bool ShouldRun() =>
            GraphicsSettings.currentRenderPipeline is AAAARenderPipelineAsset &&
            !context.assetPath.StartsWith("Packages/");
    }
}