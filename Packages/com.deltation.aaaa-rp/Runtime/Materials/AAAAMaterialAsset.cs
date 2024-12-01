using UnityEngine;

namespace DELTation.AAAARP.Materials
{
    [CreateAssetMenu(menuName = "AAAA RP/Material", fileName = "New AAAA Material")]
    public class AAAAMaterialAsset : ScriptableObject
    {
        [Header("Surface")]
        public bool AlphaClip;
        [Range(0.0f, 1.0f)]
        public float AlphaClipThreshold = 0.5f;
        public bool TwoSided;

        [Header("Color")]
        public Texture2D Albedo;
        public Color AlbedoColor = Color.white;
        public Vector4 TextureTilingOffset = new(1, 1, 0, 0);
        [ColorUsage(hdr: true, showAlpha: false)]
        public Color Emission = Color.black;

        [Header("Normals")]
        public Texture2D Normals;
        [Range(0.0f, 10.0f)]
        public float NormalsStrength = 1.0f;

        [Header("PBR")]
        public Texture2D Masks;
        [Range(0.0f, 2.0f)]
        public float Roughness = 0.5f;
        [Range(0.0f, 2.0f)]
        public float Metallic;

        [Header("Misc")]
        public bool DisableLighting;
        public bool SpecularAA;
        [Range(0.0f, 1.0f)]
        public float SpecularAAScreenSpaceVariance = 0.1f;
        [Range(0.0f, 1.0f)]
        public float SpecularAAThreshold = 0.2f;
    }
}