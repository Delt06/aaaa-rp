using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP
{
    public enum AAAAAntiAliasingTechnique
    {
        Off,
        SMAA,
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [ExecuteAlways]
    public class AAAAAdditionalCameraData : MonoBehaviour, IAdditionalData
    {
        public enum SMAAPreset
        {
            Low,
            Medium,
            High,
            Ultra,
        }

        [SerializeField] [EnumButtons] private AAAAAntiAliasingTechnique _antiAliasing = AAAAAntiAliasingTechnique.Off;
        [SerializeField] private SMAASettings _smaaSettings = new();

        public AAAAAntiAliasingTechnique AntiAliasing
        {
            get => _antiAliasing;
            set => _antiAliasing = value;
        }

        public SMAASettings SMAA
        {
            get => _smaaSettings;
            set => _smaaSettings = value;
        }

        public static AAAAAdditionalCameraData GetOrAdd(Camera camera)
        {
            GameObject gameObject = camera.gameObject;
            if (!gameObject.TryGetComponent(out AAAAAdditionalCameraData cameraData))
            {
                cameraData = gameObject.AddComponent<AAAAAdditionalCameraData>();
            }

            return cameraData;
        }

        [Serializable]
        public class SMAASettings
        {
            public SMAAPreset Preset = SMAAPreset.High;
        }
    }
}