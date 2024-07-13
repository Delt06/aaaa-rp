using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Utils
{
    [GenerateHLSL]
    public static class AAAARawBufferClear
    {
        public const int KernelIndex = 0;
        public const int ThreadGroupSize = 32;

        public static void DispatchClear(CommandBuffer cmd, ComputeShader computeShader, GraphicsBuffer buffer, int itemCount, int writeOffset, int clearValue)
        {
            cmd.SetComputeBufferParam(computeShader, KernelIndex, ShaderID._Buffer, buffer);
            cmd.SetComputeIntParam(computeShader, ShaderID._ItemCount, itemCount);
            cmd.SetComputeIntParam(computeShader, ShaderID._WriteOffset, writeOffset);
            cmd.SetComputeIntParam(computeShader, ShaderID._ClearValue, clearValue);
            cmd.DispatchCompute(computeShader, KernelIndex, AAAAMathUtils.AlignUp(itemCount, ThreadGroupSize) / ThreadGroupSize, 1, 1);
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public static class ShaderID
        {
            public static readonly int _Buffer = Shader.PropertyToID(nameof(_Buffer));
            public static readonly int _ItemCount = Shader.PropertyToID(nameof(_ItemCount));
            public static readonly int _WriteOffset = Shader.PropertyToID(nameof(_WriteOffset));
            public static readonly int _ClearValue = Shader.PropertyToID(nameof(_ClearValue));
        }
    }
}