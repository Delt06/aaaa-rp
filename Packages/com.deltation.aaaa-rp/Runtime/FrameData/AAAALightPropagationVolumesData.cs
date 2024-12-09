using Unity.Mathematics;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.FrameData
{
    public class AAAALightPropagationVolumesData : ContextItem
    {
        public TextureHandle GridBlockingPotentialSH;
        public TextureHandle GridBlueSH;
        public float3 GridBoundsMax;
        public float3 GridBoundsMin;
        public TextureHandle GridGreenSH;
        public TextureHandle GridRedSH;
        public TextureDesc GridSHDesc;
        public int GridSize;

        public override void Reset()
        {
            GridSHDesc = default;
            GridRedSH = GridGreenSH = GridBlueSH = GridBlockingPotentialSH = default;
            GridBoundsMin = default;
            GridBoundsMax = default;
            GridSize = default;
        }
    }
}