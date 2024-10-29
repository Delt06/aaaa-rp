using System;
using System.Collections.Generic;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.Materials;
using DELTation.AAAARP.Meshlets;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace DELTation.AAAARP.Renderers
{
    internal sealed class InstanceDataBuffer : IDisposable
    {
        public const int Capacity = 512;
        private readonly MaterialDataBuffer _materialDataBuffer;

        private readonly AAAARendererContainer _rendererContainer;

        private NativeArray<AAAAInstanceData> _cpuBuffer;
        private GraphicsBuffer _gpuBuffer;
        private AAAAIndexAllocator _indexAllocator;
        private bool _isDirty;
        private NativeHashMap<int, InstanceMetadata> _metadata;

        public InstanceDataBuffer(AAAARendererContainer rendererContainer, MaterialDataBuffer materialDataBuffer, Allocator allocator)
        {
            _materialDataBuffer = materialDataBuffer;
            _rendererContainer = rendererContainer;
            _cpuBuffer = new NativeArray<AAAAInstanceData>(Capacity, allocator);
            _gpuBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Capacity, UnsafeUtility.SizeOf<AAAAInstanceData>())
            {
                name = nameof(RendererContainerShaderIDs._InstanceData),
            };
            _metadata = new NativeHashMap<int, InstanceMetadata>(Capacity, allocator);
            _indexAllocator = new AAAAIndexAllocator(Capacity, allocator);
            _isDirty = true;
        }

        public int InstanceCount => _metadata.Count;

        public void Dispose()
        {
            if (_cpuBuffer.IsCreated)
            {
                _cpuBuffer.Dispose();
            }

            _gpuBuffer?.Dispose();
            _gpuBuffer = null;

            if (_metadata.IsCreated)
            {
                _metadata.Dispose();
            }

            _indexAllocator?.Dispose();
            _indexAllocator = null;
        }

        public void OnRenderersChanged(List<Object> changed, NativeArray<int> changedIDs)
        {
            var invalidIDs = new NativeList<int>(changedIDs.Length, Allocator.Temp);

            for (int index = 0; index < changedIDs.Length; index++)
            {
                int instanceID = changedIDs[index];
                var rendererAuthoring = (AAAARendererAuthoring) changed[index];

                AAAAMaterialAsset material = rendererAuthoring.Material;
                AAAAMeshletCollectionAsset mesh = rendererAuthoring.Mesh;
                if (material == null || mesh == null)
                {
                    invalidIDs.Add(instanceID);
                    continue;
                }

                bool isNew;

                if (!_metadata.TryGetValue(instanceID, out InstanceMetadata instanceMetadata))
                {
                    isNew = true;
                    AAAAIndexAllocator.IndexAllocation indexAllocation = _indexAllocator.Allocate();
                    Assert.IsTrue(indexAllocation.Index != AAAAIndexAllocator.InvalidAllocationIndex, "Instance allocation failure. Out of memory.");

                    instanceMetadata = new InstanceMetadata
                    {
                        IndexAllocation = indexAllocation,
                    };

                    _metadata.Add(instanceID, instanceMetadata);
                }
                else
                {
                    isNew = false;
                }

                ref AAAAInstanceData instanceData = ref _cpuBuffer.ElementAtRef(instanceMetadata.IndexAllocation.Index);

                if (isNew)
                {
                    Transform transform = rendererAuthoring.transform;
                    float4x4 localToWorldMatrix = transform.localToWorldMatrix;

                    instanceData.ObjectToWorldMatrix = localToWorldMatrix;
                    instanceData.WorldToObjectMatrix = AAAAMathUtils.AffineInverse3D(localToWorldMatrix);
                    instanceData.Flags = AAAAInstanceFlags.None;
                    UpdateWindingOrderFlag(ref instanceData.Flags, transform.lossyScale);
                }
                else
                {
                    _rendererContainer.MaxMeshletListBuildJobCount -= ComputeMeshletListBuildJobCount(instanceData);
                }

                AAAARendererContainer.MeshMetadata meshMetadata = _rendererContainer.GetOrAllocateMeshLODNodes(mesh);

                instanceData.AABBMin = math.float4(mesh.Bounds.min, 0.0f);
                instanceData.AABBMax = math.float4(mesh.Bounds.max, 0.0f);
                instanceData.TopMeshLODStartIndex = (uint) meshMetadata.TopMeshLODNodesStartIndex;
                instanceData.TotalMeshLODCount = (uint) mesh.MeshLODNodes.Length;
                instanceData.MaterialIndex = (uint) _materialDataBuffer.GetOrAllocateMaterial(material);
                instanceData.MeshLODLevelCount = (uint) mesh.MeshLODLevelCount;
                instanceData.LODErrorScale = rendererAuthoring.LODErrorScale;
                instanceData.PassMask = ExtractInstancePassMask(rendererAuthoring);

                instanceMetadata.MeshInstanceID = mesh.GetInstanceID();

                _rendererContainer.MaxMeshletListBuildJobCount += ComputeMeshletListBuildJobCount(instanceData);
                _isDirty = true;

                _metadata[instanceID] = instanceMetadata;
            }

            if (invalidIDs.Length > 0)
            {
                OnRenderersDestroyed(invalidIDs.AsArray());
            }

            invalidIDs.Dispose();
        }

        private static void UpdateWindingOrderFlag(ref AAAAInstanceFlags flags, float3 scale)
        {
            int negativeDimensionsCount = (int) math.dot(math.float3(scale < 0.0f), math.float3(1, 1, 1));
            if (negativeDimensionsCount % 2 == 1)
            {
                flags |= AAAAInstanceFlags.FlipWindingOrder;
            }
            else
            {
                flags &= ~AAAAInstanceFlags.FlipWindingOrder;
            }
        }

        private static AAAAInstancePassMask ExtractInstancePassMask(AAAARendererAuthoring rendererAuthoring)
        {
            AAAAInstancePassMask passMask = AAAAInstancePassMask.Main;

            switch (rendererAuthoring.ShadowCastingMode)
            {
                case ShadowCastingMode.Off:
                    break;
                case ShadowCastingMode.On:
                    passMask |= AAAAInstancePassMask.Shadows;
                    break;
                case ShadowCastingMode.TwoSided:
                    passMask |= AAAAInstancePassMask.Shadows;
                    break;
                case ShadowCastingMode.ShadowsOnly:
                    passMask = AAAAInstancePassMask.Shadows;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return passMask;
        }

        public void OnRendererTransformsChanged(NativeArray<int> transformedID, NativeArray<float4x4> localToWorldMatrices)
        {
            for (int index = 0; index < transformedID.Length; index++)
            {
                int instanceID = transformedID[index];
                if (!_metadata.TryGetValue(instanceID, out InstanceMetadata metadata))
                {
                    continue;
                }

                ref AAAAInstanceData instanceData = ref _cpuBuffer.ElementAtRef(metadata.IndexAllocation.Index);
                instanceData.ObjectToWorldMatrix = localToWorldMatrices[index];
                instanceData.WorldToObjectMatrix = AAAAMathUtils.AffineInverse3D(localToWorldMatrices[index]);

                float3 scale = ((Matrix4x4) instanceData.ObjectToWorldMatrix).lossyScale;
                UpdateWindingOrderFlag(ref instanceData.Flags, scale);

                _isDirty = true;
            }
        }

        private static int ComputeMeshletListBuildJobCount(in AAAAInstanceData instanceData) =>
            Mathf.CeilToInt((float) instanceData.TotalMeshLODCount / AAAAMeshletListBuildJob.MaxLODNodesPerThreadGroup);

        public void OnRenderersDestroyed(NativeArray<int> destroyedIDs)
        {
            foreach (int instanceID in destroyedIDs)
            {
                if (!_metadata.TryGetValue(instanceID, out InstanceMetadata metadata))
                {
                    continue;
                }

                ref readonly AAAAInstanceData instanceData = ref _cpuBuffer.ElementAtRef(metadata.IndexAllocation.Index);
                _rendererContainer.MaxMeshletListBuildJobCount -= ComputeMeshletListBuildJobCount(instanceData);

                Assert.IsTrue(_indexAllocator.IsValidGeneration(metadata.IndexAllocation), "Detected stale index allocation.");

                _indexAllocator.Free(metadata.IndexAllocation);
                _metadata.Remove(instanceID);
                _isDirty = true;
            }
        }

        public void GetInstanceIndices(NativeList<int> indices)
        {
            foreach (KVPair<int, InstanceMetadata> kvp in _metadata)
            {
                indices.Add(kvp.Value.IndexAllocation.Index);
            }
        }

        internal void GetInstanceMetadata(NativeList<InstanceMetadata> indices)
        {
            foreach (KVPair<int, InstanceMetadata> kvp in _metadata)
            {
                indices.Add(kvp.Value);
            }
        }

        public void PreRender(CommandBuffer cmd)
        {
            if (_isDirty)
            {
                cmd.SetBufferData(_gpuBuffer, _cpuBuffer);
                _isDirty = false;
            }

            cmd.SetGlobalBuffer(RendererContainerShaderIDs._InstanceData, _gpuBuffer);
        }

        internal struct InstanceMetadata
        {
            public AAAAIndexAllocator.IndexAllocation IndexAllocation;
            public int MeshInstanceID;
        }
    }
}