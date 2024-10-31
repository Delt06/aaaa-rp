using System;
using System.Collections.Generic;
using DELTation.AAAARP.Debugging;
using DELTation.AAAARP.RenderPipelineResources;
using DELTation.AAAARP.Utils;

namespace DELTation.AAAARP.Passes.Shadows
{
    internal sealed class ShadowPassPool : IDisposable
    {
        private const string NameTag = "Shadows";

        private readonly List<GPUCullingPass> _cullingPasses = new();
        private readonly AAAARenderPipelineDebugDisplaySettings _debugSettings;
        private readonly List<DrawShadowsPass> _drawShadowsPasses = new();
        private readonly AAAARawBufferClear _rawBufferClear;
        private readonly AAAARenderPassEvent _renderPassEvent;
        private readonly AAAARenderPipelineRuntimeShaders _shaders;
        private int _cullingPassOffset;
        private int _drawPassOffset;

        public ShadowPassPool(AAAARenderPassEvent renderPassEvent,
            AAAARenderPipelineRuntimeShaders shaders, AAAARawBufferClear rawBufferClear, AAAARenderPipelineDebugDisplaySettings debugSettings)
        {
            _debugSettings = debugSettings;
            _shaders = shaders;
            _rawBufferClear = rawBufferClear;
            _renderPassEvent = renderPassEvent;
        }

        public void Dispose()
        {
            foreach (GPUCullingPass gpuCullingPass in _cullingPasses)
            {
                gpuCullingPass.Dispose();
            }

            _drawShadowsPasses.Clear();
            _cullingPasses.Clear();
        }

        public DrawShadowsPass RequestDrawPass(int shadowLightIndex, int splitIndex, int contextIndex)
        {
            while (_drawPassOffset >= _drawShadowsPasses.Count)
            {
                _drawShadowsPasses.Add(new DrawShadowsPass(_renderPassEvent));
            }

            DrawShadowsPass pass = _drawShadowsPasses[_drawPassOffset];
            pass.ShadowLightIndex = shadowLightIndex;
            pass.SplitIndex = splitIndex;
            pass.ContextIndex = contextIndex;
            ++_drawPassOffset;
            return pass;
        }

        public GPUCullingPass RequestCullingPass(List<GPUCullingPass.CullingViewParameters> cullingContexts)
        {
            while (_cullingPassOffset >= _cullingPasses.Count)
            {
                _cullingPasses.Add(new GPUCullingPass(GPUCullingPass.PassType.Basic, _renderPassEvent, _shaders, _rawBufferClear, _debugSettings, NameTag));
            }

            GPUCullingPass pass = _cullingPasses[_cullingPassOffset];
            pass.CullingContextParameterList.Clear();
            pass.CullingContextParameterList.AddRange(cullingContexts);
            ++_cullingPassOffset;
            return pass;
        }

        public void Reset()
        {
            _drawPassOffset = 0;
            _cullingPassOffset = 0;
        }

        public struct PassSet
        {
            public GPUCullingPass GPUCullingPass;
            public DrawShadowsPass DrawShadowsPass;
        }
    }
}