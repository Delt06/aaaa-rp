﻿using System;
using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Debugging;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.Debugging
{
    public sealed class LightPropagationVolumesDebugPass : AAAARenderPass<LightPropagationVolumesDebugPass.PassData>, IDisposable
    {
        private readonly AAAARenderPipelineDebugDisplaySettings _debugDisplaySettings;
        private readonly Material _material;
        private readonly Mesh _mesh;

        public LightPropagationVolumesDebugPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineDebugShaders shaders,
            AAAARenderPipelineDebugDisplaySettings debugDisplaySettings) : base(renderPassEvent)
        {
            _material = CoreUtils.CreateEngineMaterial(shaders.LightPropagationVolumesDebugPS);
            _debugDisplaySettings = debugDisplaySettings;
            _mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
        }

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            AAAALightingData lightingData = frameData.Get<AAAALightingData>();

            Assert.IsTrue(lightingData.LPVGridRedSH.IsValid());
            passData.DebugSize = _debugDisplaySettings.RenderingSettings.LightPropagationVolumesDebugSize;
            passData.DebugIntensity = _debugDisplaySettings.RenderingSettings.LightPropagationVolumesDebugIntensity;
            passData.DebugClipDistance = _debugDisplaySettings.RenderingSettings.LightPropagationVolumesDebugClipDistance;
            passData.InstanceCountDimension = 15;
            passData.TotalInstanceCount = passData.InstanceCountDimension * passData.InstanceCountDimension * passData.InstanceCountDimension;

            passData.IndirectArgs = builder.CreateTransientBuffer(new BufferDesc
                {
                    name = nameof(LightPropagationVolumesDebugPass) + "_" + nameof(PassData.IndirectArgs),
                    count = 1,
                    stride = GraphicsBuffer.IndirectDrawIndexedArgs.size,
                    target = GraphicsBuffer.Target.IndirectArguments,
                }
            );

            builder.ReadTexture(lightingData.LPVGridRedSH);
            passData.RenderTarget = builder.ReadWriteTexture(resourceData.CameraScaledColorBuffer);
            passData.DepthStencil = builder.ReadWriteTexture(resourceData.CameraScaledDepthBuffer);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            const int subMeshIndex = 0;
            context.cmd.SetBufferData(data.IndirectArgs, new NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs>(1, Allocator.Temp)
                {
                    [0] = new GraphicsBuffer.IndirectDrawIndexedArgs
                    {
                        instanceCount = (uint) data.TotalInstanceCount,
                        startIndex = (uint) _mesh.GetSubMesh(subMeshIndex).indexStart,
                        startInstance = 0,
                        baseVertexIndex = (uint) _mesh.GetSubMesh(subMeshIndex).baseVertex,
                        indexCountPerInstance = (uint) _mesh.GetSubMesh(subMeshIndex).indexCount,
                    },
                }
            );

            context.cmd.SetRenderTarget(data.RenderTarget, data.DepthStencil);
            data.PropertyBlock.Clear();
            data.PropertyBlock.SetInteger(ShaderID._DebugInstanceCountDimension, data.InstanceCountDimension);
            data.PropertyBlock.SetFloat(ShaderID._DebugSize, data.DebugSize);
            data.PropertyBlock.SetFloat(ShaderID._DebugIntensity, data.DebugIntensity);
            data.PropertyBlock.SetFloat(ShaderID._DebugClipDistance, data.DebugClipDistance);
            context.cmd.DrawMeshInstancedIndirect(_mesh, subMeshIndex, _material, 0, data.IndirectArgs, 0, data.PropertyBlock);
        }

        public class PassData : PassDataBase
        {
            public readonly MaterialPropertyBlock PropertyBlock = new();
            public float DebugClipDistance;
            public float DebugIntensity;
            public float DebugSize;
            public TextureHandle DepthStencil;
            public BufferHandle IndirectArgs;
            public int InstanceCountDimension;
            public TextureHandle RenderTarget;
            public int TotalInstanceCount;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderID
        {
            public static int _DebugInstanceCountDimension = Shader.PropertyToID(nameof(_DebugInstanceCountDimension));
            public static int _DebugSize = Shader.PropertyToID(nameof(_DebugSize));
            public static int _DebugIntensity = Shader.PropertyToID(nameof(_DebugIntensity));
            public static int _DebugClipDistance = Shader.PropertyToID(nameof(_DebugClipDistance));
        }
    }
}