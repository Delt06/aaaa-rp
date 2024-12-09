using System;
using System.Collections.Generic;
using DELTation.AAAARP.Core.ObjectDispatching;
using DELTation.AAAARP.Materials;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DELTation.AAAARP.Renderers
{
    internal class AAAAObjectTracker : IDisposable
    {
        private readonly AuthoringTracker _authoringTracker;
        private readonly MaterialTracker _materialTracker;
        private readonly TextureTracker _textureTracker;

        internal AAAAObjectTracker(InstanceDataBuffer instanceDataBuffer, MaterialDataBuffer materialDataBuffer,
            BindlessTextureContainer bindlessTextureContainer)
        {
            _authoringTracker = new AuthoringTracker(instanceDataBuffer, ObjectDispatcherService.TypeTrackingFlags.SceneObjects);
            ObjectDispatcherService.RegisterObjectTracker(_authoringTracker);

            _textureTracker = new TextureTracker(bindlessTextureContainer, ObjectDispatcherService.TypeTrackingFlags.Assets);
            ObjectDispatcherService.RegisterObjectTracker(_textureTracker);

            _materialTracker = new MaterialTracker(materialDataBuffer, ObjectDispatcherService.TypeTrackingFlags.Assets);
            ObjectDispatcherService.RegisterObjectTracker(_materialTracker);
        }

        public void Dispose()
        {
            ObjectDispatcherService.UnregisterObjectTracker(_authoringTracker);
            ObjectDispatcherService.UnregisterObjectTracker(_textureTracker);
            ObjectDispatcherService.UnregisterObjectTracker(_materialTracker);
        }

        private class AuthoringTracker : ObjectTracker<AAAARendererAuthoring>, IObjectTransformTracker
        {
            private readonly InstanceDataBuffer _instanceDataBuffer;

            public AuthoringTracker(InstanceDataBuffer instanceDataBuffer, ObjectDispatcherService.TypeTrackingFlags trackingFlags) : base(trackingFlags) =>
                _instanceDataBuffer = instanceDataBuffer;

            public void ProcessTransformData(NativeArray<int> transformedID, NativeArray<int> parentID, NativeArray<Matrix4x4> localToWorldMatrices,
                NativeArray<Vector3> positions, NativeArray<Quaternion> rotations,
                NativeArray<Vector3> scales)
            {
                if (transformedID.Length > 0)
                {
                    _instanceDataBuffer.OnRendererTransformsChanged(transformedID, localToWorldMatrices.Reinterpret<float4x4>());
                }
            }

            public override void ProcessData(List<Object> changed, NativeArray<int> changedID, NativeArray<int> destroyedID)
            {
                _instanceDataBuffer.OnRenderersDestroyed(destroyedID);
                _instanceDataBuffer.OnRenderersChanged(changed, changedID);
            }
        }

        private class TextureTracker : ObjectTracker<Texture2D>
        {
            private readonly BindlessTextureContainer _bindlessTextureContainer;

            public TextureTracker(BindlessTextureContainer bindlessTextureContainer, ObjectDispatcherService.TypeTrackingFlags trackingFlags) :
                base(trackingFlags) => _bindlessTextureContainer = bindlessTextureContainer;

            public override void ProcessData(List<Object> changed, NativeArray<int> changedID, NativeArray<int> destroyedID)
            {
                _bindlessTextureContainer.AddPotentialDirtyTextureRange(changedID, changed);
                _bindlessTextureContainer.AddPotentialDestroyedDirtyTextureRange(destroyedID);
            }
        }

        private class MaterialTracker : ObjectTracker<AAAAMaterialAsset>
        {
            private readonly MaterialDataBuffer _materialDataBuffer;

            public MaterialTracker(MaterialDataBuffer materialDataBuffer, ObjectDispatcherService.TypeTrackingFlags trackingFlags) : base(trackingFlags) =>
                _materialDataBuffer = materialDataBuffer;

            public override void ProcessData(List<Object> changed, NativeArray<int> changedID, NativeArray<int> destroyedID)
            {
                if (changed.Count > 0)
                {
                    _materialDataBuffer.OnMaterialAssetsChanged(changed, changedID);
                }
            }
        }
    }
}