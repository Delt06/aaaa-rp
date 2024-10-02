using UnityEngine;

namespace DELTation.AAAARP.Materials
{
    [CreateAssetMenu(menuName = "AAAA RP/Material", fileName = "New AAAA Material")]
    public class AAAAMaterialAsset : ScriptableObject
    {
        public Texture2D Albedo;
        public Color AlbedoColor = Color.white;
        public Vector4 TextureTilingOffset = new(1, 1, 0, 0);

        public Texture2D Normals;
        [Range(0.0f, 10.0f)]
        public float NormalsStrength = 1.0f;

        public Texture2D Masks;
        [Range(0.0f, 1.0f)]
        public float Roughness = 0.5f;
        [Range(0.0f, 1.0f)]
        public float Metallic;
    }
}