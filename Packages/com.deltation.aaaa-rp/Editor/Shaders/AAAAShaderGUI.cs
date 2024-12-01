using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace DELTation.AAAARP.Editor.Shaders
{
    [UsedImplicitly]
    public class AAAAShaderGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            base.OnGUI(materialEditor, properties);

            BakedEmission(materialEditor);
        }

        private static void BakedEmission(MaterialEditor materialEditor)
        {
            EditorGUI.BeginChangeCheck();
            materialEditor.LightmapEmissionProperty();
            if (EditorGUI.EndChangeCheck())
            {
                foreach (Object target in materialEditor.targets)
                {
                    if (target is Material material)
                    {
                        material.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                    }
                }
            }
        }
    }
}