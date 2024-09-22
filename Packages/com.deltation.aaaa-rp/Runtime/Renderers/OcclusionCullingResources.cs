using System;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.Utils;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Renderers
{
    internal class OcclusionCullingResources : IDisposable
    {
        private const int FramesInFlight = 3;
        private readonly FrameResources[] _frameResources;
        private readonly ComputeShader _rawBufferClearCS;

        private int _frameIndex;
        private bool _isDirty;

        public OcclusionCullingResources(ComputeShader rawBufferClearCS)
        {
            _frameResources = new FrameResources[FramesInFlight];
            _rawBufferClearCS = rawBufferClearCS;

            const int stride = sizeof(uint);
            const int instancesPerItem = stride * 8;
            InstanceVisibilityMaskItemCount = AAAAMathUtils.AlignUp(InstanceDataBuffer.Capacity, instancesPerItem) / instancesPerItem;

            for (int index = 0; index < FramesInFlight; index++)
            {
                ref FrameResources frameResources = ref _frameResources[index];

                frameResources.InstanceVisibilityMask = new GraphicsBuffer(GraphicsBuffer.Target.Raw, InstanceVisibilityMaskItemCount, stride)
                {
                    name = $"{nameof(OcclusionCullingResources)}_{nameof(FrameResources.InstanceVisibilityMask)}[{index}]",
                };
            }

            _isDirty = true;
        }

        public int InstanceVisibilityMaskItemCount { get; }

        public void Dispose()
        {
            for (int index = 0; index < _frameResources.Length; index++)
            {
                ref FrameResources frameResources = ref _frameResources[index];
                frameResources.Dispose();
            }
        }

        public ref readonly FrameResources GetCurrentFrameResources() => ref _frameResources[_frameIndex];

        public void PreRender(CommandBuffer cmd)
        {
            using (new ProfilingScope(cmd, Profiling.OcclusionCullingResources))
            {
                InvalidateBuffersIfDirty(cmd);
                BindBuffers(cmd);
            }
        }

        private void InvalidateBuffersIfDirty(CommandBuffer cmd)
        {
            if (_isDirty)
            {
                using (new ProfilingScope(cmd, Profiling.InvalidateBuffers))
                {
                    foreach (FrameResources frameResources in _frameResources)
                    {
                        GraphicsBuffer mask = frameResources.InstanceVisibilityMask;
                        const int writeOffset = 0;
                        const int clearValue = 0;
                        AAAARawBufferClear.DispatchClear(cmd, _rawBufferClearCS, mask, mask.count, writeOffset, clearValue);
                    }

                    _isDirty = false;
                }
            }
        }

        private void BindBuffers(CommandBuffer cmd)
        {
            cmd.SetGlobalBuffer(RendererContainerShaderIDs._OcclusionCulling_InstanceVisibilityMask,
                _frameResources[_frameIndex].InstanceVisibilityMask
            );
            cmd.SetGlobalBuffer(RendererContainerShaderIDs._OcclusionCulling_PrevInstanceVisibilityMask,
                _frameResources[GetPrevResourcesIndex(_frameIndex)].InstanceVisibilityMask
            );
        }

        public void PostRender()
        {
            ++_frameIndex;
            _frameIndex %= FramesInFlight;
        }

        private static int GetPrevResourcesIndex(int index)
        {
            int prevIndex = index - 1;
            if (prevIndex < 0)
            {
                prevIndex += FramesInFlight;
            }
            return prevIndex;
        }

        internal struct FrameResources : IDisposable
        {
            public GraphicsBuffer InstanceVisibilityMask;

            public void Dispose()
            {
                InstanceVisibilityMask?.Dispose();
                InstanceVisibilityMask = default;
            }
        }

        private static class Profiling
        {
            public static ProfilingSampler OcclusionCullingResources = new(nameof(OcclusionCullingResources));
            public static ProfilingSampler InvalidateBuffers = new(nameof(InvalidateBuffers));
        }
    }
}