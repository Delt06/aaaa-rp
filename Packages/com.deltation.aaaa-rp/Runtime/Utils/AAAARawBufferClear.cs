﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.RenderPipelineResources;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Utils
{
    [GenerateHLSL]
    public class AAAARawBufferClear : IDisposable
    {
        public const int KernelIndex = 0;
        public const int ThreadGroupSize = 32;

        private readonly Dictionary<int, GraphicsBuffer> _fastZeroClearBuffers = new();
        private readonly ComputeShader _rawBufferClearCS;

        public AAAARawBufferClear(AAAARenderPipelineRuntimeShaders shaders) => _rawBufferClearCS = shaders.RawBufferClearCS;

        public void Dispose()
        {
            foreach (GraphicsBuffer graphicsBuffer in _fastZeroClearBuffers.Values)
            {
                graphicsBuffer.Dispose();
            }
            _fastZeroClearBuffers.Clear();
        }

        public void FastZeroClear(CommandBuffer cmd, GraphicsBuffer buffer, int itemCount)
        {
            if (!_fastZeroClearBuffers.TryGetValue(itemCount, out GraphicsBuffer clearBuffer))
            {
                clearBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw | GraphicsBuffer.Target.CopySource, itemCount, sizeof(uint));
                var zeroes = new NativeArray<int>(itemCount, Allocator.Temp);
                clearBuffer.SetData(zeroes);
                _fastZeroClearBuffers.Add(itemCount, clearBuffer);
            }

            cmd.CopyBuffer(clearBuffer, buffer);
        }

        public void DispatchClear(CommandBuffer cmd, GraphicsBuffer buffer, int itemCount, int writeOffset, int clearValue)
        {
            cmd.SetComputeBufferParam(_rawBufferClearCS, KernelIndex, ShaderID._Buffer, buffer);
            cmd.SetComputeIntParam(_rawBufferClearCS, ShaderID._ItemCount, itemCount);
            cmd.SetComputeIntParam(_rawBufferClearCS, ShaderID._WriteOffset, writeOffset);
            cmd.SetComputeIntParam(_rawBufferClearCS, ShaderID._ClearValue, clearValue);
            cmd.DispatchCompute(_rawBufferClearCS, KernelIndex, AAAAMathUtils.AlignUp(itemCount, ThreadGroupSize) / ThreadGroupSize, 1, 1);
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderID
        {
            public static readonly int _Buffer = Shader.PropertyToID(nameof(_Buffer));
            public static readonly int _ItemCount = Shader.PropertyToID(nameof(_ItemCount));
            public static readonly int _WriteOffset = Shader.PropertyToID(nameof(_WriteOffset));
            public static readonly int _ClearValue = Shader.PropertyToID(nameof(_ClearValue));
        }
    }
}