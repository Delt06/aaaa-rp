using System;
using System.Collections.Generic;
using DELTation.AAAARP.Core.ObjectDispatching;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DELTation.AAAARP.Renderers
{
    internal class AAAAObjectTracker : IDisposable
    {
        private readonly AuthoringTracker _authoringTracker;
        private readonly TextureTracker _textureTracker;

        internal AAAAObjectTracker(InstanceDataBuffer instanceDataBuffer, BindlessTextureContainer bindlessTextureContainer)
        {
            _authoringTracker = new AuthoringTracker(instanceDataBuffer, ObjectDispatcherService.TypeTrackingFlags.SceneObjects);
            ObjectDispatcherService.RegisterObjectTracker(_authoringTracker);

            _textureTracker = new TextureTracker(bindlessTextureContainer, ObjectDispatcherService.TypeTrackingFlags.Assets);
            ObjectDispatcherService.RegisterObjectTracker(_textureTracker);
        }

        public void Dispose()
        {
            ObjectDispatcherService.UnregisterObjectTracker(_authoringTracker);
            ObjectDispatcherService.UnregisterObjectTracker(_textureTracker);
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
                _instanceDataBuffer.OnRenderersChanged(changed, changedID);
                _instanceDataBuffer.OnRenderersDestroyed(destroyedID);
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
    }
}