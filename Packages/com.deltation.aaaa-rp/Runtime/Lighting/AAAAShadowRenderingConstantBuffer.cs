using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Lighting
{
    [GenerateHLSL(PackingRules.Exact, needAccessors = false, generateCBuffer = true)]
    public struct AAAAShadowRenderingConstantBuffer
    {
        public Matrix4x4 ShadowProjectionMatrix;
        public Matrix4x4 ShadowViewMatrix;
        public Matrix4x4 ShadowViewProjection;
        public Vector4 ShadowLightDirection;
        public Vector4 ShadowBiases;
    }
}