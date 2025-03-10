﻿using System;
using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Debugging;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.Debugging
{
    public sealed class VXGIDebugPass : AAAARenderPass<VXGIDebugPass.PassData>, IDisposable
    {
        private readonly AAAARenderPipelineDebugDisplaySettings _debugDisplaySettings;
        private readonly Material _material;
        private readonly Mesh _mesh;

        public VXGIDebugPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineDebugShaders shaders,
            AAAARenderPipelineDebugDisplaySettings debugDisplaySettings) : base(renderPassEvent)
        {
            _material = CoreUtils.CreateEngineMaterial(shaders.VXGIDebugPS);
            _mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            _debugDisplaySettings = debugDisplaySettings;
        }

        public override string Name => "VXGI.Debug";

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
        }

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            AAAAVoxelGlobalIlluminationData vxgiData = frameData.Get<AAAAVoxelGlobalIlluminationData>();
            AAAADebugDisplaySettingsRendering renderingSettings = _debugDisplaySettings.RenderingSettings;

            passData.DebugMode = renderingSettings.VXGIDebugMode;
            passData.GridMipLevel = math.min(vxgiData.GridMipCount - 1, renderingSettings.VXGIMipLevel);
            int mipGridSize = vxgiData.GridSize >> passData.GridMipLevel;
            passData.TotalInstanceCount = mipGridSize * mipGridSize * mipGridSize;
            passData.Overlay = renderingSettings.VXGIDebugOverlay;
            passData.IndirectArgs = builder.CreateTransientBuffer(new BufferDesc
                {
                    name = nameof(VXGIDebugPass) + "_" + nameof(PassData.IndirectArgs),
                    count = 1,
                    stride = GraphicsBuffer.IndirectDrawIndexedArgs.size,
                    target = GraphicsBuffer.Target.IndirectArguments,
                }
            );

            passData.GridRadiance = builder.ReadTexture(vxgiData.GridRadiance);
            passData.GridNormals = builder.ReadTexture(vxgiData.GridNormals);
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
            if (!data.Overlay)
            {
                context.cmd.ClearRenderTarget(RTClearFlags.All, Color.black);
            }
            data.PropertyBlock.Clear();
            data.PropertyBlock.SetInteger(ShaderID._DebugMode, (int) data.DebugMode);
            data.PropertyBlock.SetInteger(ShaderID._GridMipLevel, data.GridMipLevel);
            data.PropertyBlock.SetTexture(ShaderID._GridRadiance, data.GridRadiance);
            data.PropertyBlock.SetTexture(ShaderID._GridNormals, data.GridNormals);
            context.cmd.DrawMeshInstancedIndirect(_mesh, subMeshIndex, _material, 0, data.IndirectArgs, 0, data.PropertyBlock);
        }

        public class PassData : PassDataBase
        {
            public readonly MaterialPropertyBlock PropertyBlock = new();
            public AAAAVxgiDebugMode DebugMode;
            public TextureHandle DepthStencil;
            public int GridMipLevel;
            public TextureHandle GridNormals;
            public TextureHandle GridRadiance;
            public BufferHandle IndirectArgs;
            public bool Overlay;
            public TextureHandle RenderTarget;
            public int TotalInstanceCount;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderID
        {
            public static int _DebugMode = Shader.PropertyToID(nameof(_DebugMode));
            public static int _GridMipLevel = Shader.PropertyToID(nameof(_GridMipLevel));
            public static int _GridRadiance = Shader.PropertyToID(nameof(_GridRadiance));
            public static int _GridNormals = Shader.PropertyToID(nameof(_GridNormals));
        }
    }
}