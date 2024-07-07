using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP
{
    internal static class CameraMetadataCache
    {
        private static readonly Dictionary<Camera, CameraMetadata> Cache = new();
        
        public static CameraMetadata Get(Camera camera)
        {
            if (Cache.TryGetValue(camera, out CameraMetadata metadata))
            {
                return metadata;
            }
            
            string cameraName = camera.name;
            metadata = new CameraMetadata
            {
                Name = cameraName,
                Sampler = new ProfilingSampler(cameraName),
            };
            Cache.Add(camera, metadata);
            return metadata;
        }
    }
}