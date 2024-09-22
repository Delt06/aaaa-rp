using System;
using System.Collections.Generic;
using DELTation.AAAARP.Core.ObjectDispatching;
using Unity.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DELTation.AAAARP.Renderers
{
    public class AAAAObjectTracker : IDisposable
    {
        private readonly AuthoringTracker _authoringTracker;
        private readonly TextureTracker _textureTracker;

        internal AAAAObjectTracker(BindlessTextureContainer bindlessTextureContainer)
        {
            _authoringTracker = new AuthoringTracker(ObjectDispatcherService.TypeTrackingFlags.SceneObjects);
            ObjectDispatcherService.RegisterObjectTracker(_authoringTracker);

            _textureTracker = new TextureTracker(bindlessTextureContainer, ObjectDispatcherService.TypeTrackingFlags.Assets);
            ObjectDispatcherService.RegisterObjectTracker(_textureTracker);
        }

        public void Dispose()
        {
            ObjectDispatcherService.UnregisterObjectTracker(_authoringTracker);
            ObjectDispatcherService.UnregisterObjectTracker(_textureTracker);
        }

        private class AuthoringTracker : ObjectTracker<AAAARendererAuthoring>
        {
            public AuthoringTracker(ObjectDispatcherService.TypeTrackingFlags trackingFlags) : base(trackingFlags) { }

            public override void ProcessData(List<Object> changed, NativeArray<int> changedID, NativeArray<int> destroyedID) { }
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