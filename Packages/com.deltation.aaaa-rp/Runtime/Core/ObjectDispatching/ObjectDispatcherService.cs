#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using Object = UnityEngine.Object;

namespace DELTation.AAAARP.Core.ObjectDispatching
{
    internal class ObjectDispatcherService : IDisposable
    {
        public enum TransformTrackingType
        {
            GlobalTRS,
            LocalTRS,
            Hierarchy,
        }

        [Flags]
        public enum TypeTrackingFlags
        {
            SceneObjects = 1,
            Assets = 2,
            EditorOnlyObjects = 4,
            Default = 3,
            All = 7,
        }

        private const ObjectDispatcher.TransformTrackingType DefaultTransformTrackingType = ObjectDispatcher.TransformTrackingType.GlobalTRS;

        private static ObjectDispatcherService _instance;

        private static readonly Type[] TypeArray = new Type[1];

        private readonly List<Object> _changedObjects = new();
        private readonly Dictionary<Type, HashSet<IObjectTransformTracker>> _transformTrackers = new();

        private ObjectDispatcher _dispatcher;
        private Dictionary<Type, HashSet<ObjectTracker>> _objectTrackers = new();

        private ObjectDispatcherService()
        {
            _dispatcher = new ObjectDispatcher();

#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload += OnAssemblyReload;
#endif
        }

        public void Dispose()
        {
            RemoveFromPlayerLoop();

#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload -= OnAssemblyReload;
#endif

            _dispatcher.Dispose();
            _dispatcher = null;

            foreach (KeyValuePair<Type, HashSet<ObjectTracker>> item in _objectTrackers)
            {
                item.Value.Clear();
            }
            _objectTrackers.Clear();
            _objectTrackers = null;

            if (_instance == this)
            {
                _instance = null;
            }
        }

        public static void RegisterObjectTracker(ObjectTracker tracker)
        {
            if (_instance != null)
            {
                if (!_instance._objectTrackers.TryGetValue(tracker.TrackedType, out HashSet<ObjectTracker> objectTrackers))
                {
                    objectTrackers = new HashSet<ObjectTracker>();
                    _instance._objectTrackers.Add(tracker.TrackedType, objectTrackers);
                }

                if (objectTrackers.Add(tracker))
                {
                    _instance._dispatcher.EnableTypeTracking((ObjectDispatcher.TypeTrackingFlags) tracker.TrackingFlags,
                        SingleItemTypeArray(tracker.TrackedType)
                    );
                }

                if (tracker is IObjectTransformTracker transformTracker)
                {
                    if (!_instance._transformTrackers.TryGetValue(tracker.TrackedType, out HashSet<IObjectTransformTracker> transformTrackers))
                    {
                        transformTrackers = new HashSet<IObjectTransformTracker>();
                        _instance._transformTrackers.Add(tracker.TrackedType, transformTrackers);
                    }

                    if (transformTrackers.Add(transformTracker))
                    {
                        _instance._dispatcher.EnableTransformTracking(DefaultTransformTrackingType, SingleItemTypeArray(tracker.TrackedType));
                    }
                }
            }
        }

        private static Type[] SingleItemTypeArray(Type type)
        {
            TypeArray[0] = type;
            return TypeArray;
        }

        public static void UnregisterObjectTracker(ObjectTracker tracker)
        {
            if (_instance != null)
            {
                if (!_instance._dispatcher.valid)
                {
                    return;
                }

                if (_instance._objectTrackers.TryGetValue(tracker.TrackedType, out HashSet<ObjectTracker> objectTrackers))
                {
                    if (objectTrackers.Remove(tracker))
                    {
                        if (objectTrackers.Count == 0)
                        {
                            _instance._dispatcher.DisableTypeTracking(tracker.TrackedType);
                        }
                    }
                }

                if (tracker is IObjectTransformTracker transformTracker)
                {
                    if (_instance._transformTrackers.TryGetValue(tracker.TrackedType, out HashSet<IObjectTransformTracker> transformTrackers))
                    {
                        if (transformTrackers.Remove(transformTracker))
                        {
                            if (transformTrackers.Count == 0)
                            {
                                _instance._dispatcher.DisableTransformTracking(DefaultTransformTrackingType, tracker.TrackedType);
                            }
                        }
                    }
                }
            }
        }


#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
#endif
        private static void Init()
        {
            _instance = new ObjectDispatcherService();
            _instance.InsertIntoPlayerLoop();
        }

        private void InsertIntoPlayerLoop()
        {
            PlayerLoopSystem rootLoop = PlayerLoop.GetCurrentPlayerLoop();
            bool isAdded = false;

            for (int i = 0; i < rootLoop.subSystemList.Length; i++)
            {
                PlayerLoopSystem subSystem = rootLoop.subSystemList[i];

                // We have to update inside the PostLateUpdate systems, because we have to be able to get previous matrices from renderers.
                // Previous matrices are updated by renderer managers on UpdateAllRenderers which is part of PostLateUpdate.
                if (!isAdded && subSystem.type == typeof(PostLateUpdate))
                {
                    var subSubSystems = new List<PlayerLoopSystem>();
                    foreach (PlayerLoopSystem subSubSystem in subSystem.subSystemList)
                    {
                        if (subSubSystem.type == typeof(PostLateUpdate.FinishFrameRendering))
                        {
                            PlayerLoopSystem s = default;
                            s.updateDelegate += OnUpdate;
                            s.type = GetType();
                            subSubSystems.Add(s);
                            isAdded = true;
                        }

                        subSubSystems.Add(subSubSystem);
                    }

                    subSystem.subSystemList = subSubSystems.ToArray();
                    rootLoop.subSystemList[i] = subSystem;
                }
            }

            PlayerLoop.SetPlayerLoop(rootLoop);
        }

        private void RemoveFromPlayerLoop()
        {
            PlayerLoopSystem rootLoop = PlayerLoop.GetCurrentPlayerLoop();

            for (int i = 0; i < rootLoop.subSystemList.Length; i++)
            {
                PlayerLoopSystem subsystem = rootLoop.subSystemList[i];
                if (subsystem.type != typeof(PostLateUpdate))
                    continue;

                var newList = new List<PlayerLoopSystem>();
                foreach (PlayerLoopSystem subSubSystem in subsystem.subSystemList)
                {
                    if (subSubSystem.type != GetType())
                        newList.Add(subSubSystem);
                }
                subsystem.subSystemList = newList.ToArray();
                rootLoop.subSystemList[i] = subsystem;
            }
            PlayerLoop.SetPlayerLoop(rootLoop);
        }

        private void OnAssemblyReload()
        {
            Dispose();
        }

        private void OnUpdate()
        {
            foreach (KeyValuePair<Type, HashSet<ObjectTracker>> typeTrackers in _objectTrackers)
            {
                if (typeTrackers.Value.Count > 0)
                {
                    _changedObjects.Clear();
                    _dispatcher.GetTypeChangesAndClear(typeTrackers.Key, _changedObjects, out NativeArray<int> changedID, out NativeArray<int> destroyedID,
                        Allocator.Temp
                    );

                    foreach (ObjectTracker tracker in typeTrackers.Value)
                    {
                        tracker.ProcessData(_changedObjects, changedID, destroyedID);
                    }

                    changedID.Dispose();
                    destroyedID.Dispose();
                    _changedObjects.Clear();
                }
            }

            foreach (KeyValuePair<Type, HashSet<IObjectTransformTracker>> transformTrackers in _transformTrackers)
            {
                if (transformTrackers.Value.Count > 0)
                {
                    TransformDispatchData changeData =
                        _dispatcher.GetTransformChangesAndClear(transformTrackers.Key, DefaultTransformTrackingType, Allocator.Temp);

                    foreach (IObjectTransformTracker transformTracker in transformTrackers.Value)
                    {
                        transformTracker.ProcessTransformData(changeData.transformedID, changeData.parentID, changeData.localToWorldMatrices,
                            changeData.positions, changeData.rotations, changeData.scales
                        );
                    }

                    changeData.Dispose();
                }
            }

            OnUpdated?.Invoke();
        }

        public static event Action OnUpdated;

        [CanBeNull]
        public static T FindByInstanceId<T>(int instanceId) where T : Object => Object.FindObjectFromInstanceID(instanceId) as T;

        public static void ProcessUpdates()
        {
            _instance.OnUpdate();
        }
    }
}