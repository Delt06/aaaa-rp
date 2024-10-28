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

        private readonly AAAARenderPipelineDebugDisplaySettings _debugSettings;
        private readonly AAAARawBufferClear _rawBufferClear;
        private readonly AAAARenderPassEvent _renderPassEvent;
        private readonly List<PassSet> _sets = new();
        private readonly AAAARenderPipelineRuntimeShaders _shaders;
        private int _setsOffset;

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
            foreach (PassSet set in _sets)
            {
                set.GPUCullingPass.Dispose();
            }

            _sets.Clear();
        }

        public PassSet RequestPassesBasic(int shadowLightIndex, int splitIndex, in GPUCullingPass.CullingViewParameters cullingViewParameters, int contextIndex)
        {
            while (_setsOffset >= _sets.Count)
            {
                _sets.Add(new PassSet
                    {
                        GPUCullingPass =
                            new GPUCullingPass(GPUCullingPass.PassType.Basic, _renderPassEvent, _shaders, _rawBufferClear, _debugSettings, NameTag),
                        DrawShadowsPass = new DrawShadowsPass(_renderPassEvent),
                    }
                );
            }

            PassSet set = _sets[_setsOffset];
            set.GPUCullingPass.CullingViewParametersOverride = cullingViewParameters;
            set.GPUCullingPass.ContextIndex = contextIndex;
            set.DrawShadowsPass.ShadowLightIndex = shadowLightIndex;
            set.DrawShadowsPass.SplitIndex = splitIndex;
            set.DrawShadowsPass.ContextIndex = contextIndex;
            ++_setsOffset;
            return set;
        }

        public void Reset()
        {
            _setsOffset = 0;
        }

        public struct PassSet
        {
            public GPUCullingPass GPUCullingPass;
            public DrawShadowsPass DrawShadowsPass;
        }
    }
}