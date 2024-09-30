using DELTation.AAAARP.Lighting;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.FrameData
{
    public class AAAALightingData : ContextItem
    {
        public float AmbientIntensity;
        public AAAALightingConstantBuffer LightingConstantBuffer;

        public override void Reset()
        {
            LightingConstantBuffer = default;
        }
    }
}