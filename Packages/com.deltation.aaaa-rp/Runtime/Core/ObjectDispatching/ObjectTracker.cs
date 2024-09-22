using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DELTation.AAAARP.Core.ObjectDispatching
{
    internal interface IObjectTransformTracker
    {
        void ProcessTransformData(
            NativeArray<int> transformedID,
            NativeArray<int> parentID,
            NativeArray<Matrix4x4> localToWorldMatrices,
            NativeArray<Vector3> positions,
            NativeArray<Quaternion> rotations,
            NativeArray<Vector3> scales
        );
    }

    internal abstract class ObjectTracker
    {
        public readonly Type TrackedType;

        protected ObjectTracker(Type trackedType, ObjectDispatcherService.TypeTrackingFlags trackingFlags)
        {
            TrackedType = trackedType;
            TrackingFlags = trackingFlags;
        }

        public ObjectDispatcherService.TypeTrackingFlags TrackingFlags { get; }

        public abstract void ProcessData(List<Object> changed, NativeArray<int> changedID, NativeArray<int> destroyedID);
    }

    internal abstract class ObjectTracker<T> : ObjectTracker
    {
        protected ObjectTracker(ObjectDispatcherService.TypeTrackingFlags trackingFlags) : base(typeof(T), trackingFlags) { }
    }
}