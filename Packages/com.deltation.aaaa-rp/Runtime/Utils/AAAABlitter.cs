using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Utils
{
    public static class AAAABlitter
    {
        public static void BlitTriangle(CommandBuffer cmd, Material material, int shaderPassId, MaterialPropertyBlock properties = null)
        {
            cmd.DrawProcedural(Matrix4x4.identity, material, shaderPassId, MeshTopology.Triangles, 3, 1, properties);
        }
    }
}