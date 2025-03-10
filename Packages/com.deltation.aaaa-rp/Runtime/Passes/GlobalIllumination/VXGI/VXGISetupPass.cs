﻿using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Lighting;
using DELTation.AAAARP.Utils;
using DELTation.AAAARP.Volumes;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.GlobalIllumination.VXGI
{
    public class VXGISetupPass : AAAARenderPass<VXGISetupPass.PassData>
    {
        private readonly AAAARawBufferClear _rawBufferClear;

        public VXGISetupPass(AAAARenderPassEvent renderPassEvent, AAAARawBufferClear rawBufferClear) : base(renderPassEvent) =>
            _rawBufferClear = rawBufferClear;

        public override string Name => "VXGI.Setup";

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAAVoxelGlobalIlluminationData vxgiData = frameData.GetOrCreate<AAAAVoxelGlobalIlluminationData>();

            AAAAVXGIVolumeComponent volumeComponent = cameraData.VolumeStack.GetComponent<AAAAVXGIVolumeComponent>();

            vxgiData.GridSize = (int) volumeComponent.GridSize.value;

            int packedBufferCount = vxgiData.GridSize * vxgiData.GridSize * vxgiData.GridSize * (int) AAAAVxgiPackedGridChannels.TotalCount;
            vxgiData.PackedGridBufferDesc = new BufferDesc(AAAAMathUtils.AlignUp(packedBufferCount, 4), sizeof(uint), GraphicsBuffer.Target.Raw)
            {
                name = AAAAVxgiCommon.ResourceNamePrefix + nameof(AAAAVoxelGlobalIlluminationData.PackedGridBuffer),
            };
            vxgiData.PackedGridBuffer = renderingData.RenderGraph.CreateBuffer(vxgiData.PackedGridBufferDesc);

            vxgiData.GridMipCount = math.min(AAAAVxgiCommon.MaxMipLevels, 1 + math.ceillog2(vxgiData.GridSize));
            var gridTextureDesc = new TextureDesc
            {
                width = vxgiData.GridSize,
                height = vxgiData.GridSize,
                slices = vxgiData.GridSize,
                useMipMap = true,
                dimension = TextureDimension.Tex3D,
                filterMode = FilterMode.Trilinear,
                msaaSamples = MSAASamples.None,
                enableRandomWrite = true,
            };
            vxgiData.GridRadiance = renderingData.RenderGraph.CreateTexture(new TextureDesc(gridTextureDesc)
                {
                    name = AAAAVxgiCommon.ResourceNamePrefix + nameof(AAAAVoxelGlobalIlluminationData.GridRadiance),
                    format = GraphicsFormat.R8G8B8A8_UNorm,
                }
            );
            vxgiData.GridNormals = renderingData.RenderGraph.CreateTexture(new TextureDesc(gridTextureDesc)
                {
                    name = AAAAVxgiCommon.ResourceNamePrefix + nameof(AAAAVoxelGlobalIlluminationData.GridNormals),
                    format = GraphicsFormat.R8G8_UNorm,
                }
            );

            vxgiData.RenderScale = (int) volumeComponent.RenderScale.value;
            vxgiData.IndirectDiffuseTexture = renderingData.RenderGraph.CreateTexture(CreateFullscreenIndirectTextureDesc(resourceData, vxgiData.RenderScale,
                    AAAAVxgiCommon.ResourceNamePrefix + nameof(AAAAVoxelGlobalIlluminationData.IndirectDiffuseTexture)
                )
            );
            vxgiData.IndirectSpecularTexture = renderingData.RenderGraph.CreateTexture(CreateFullscreenIndirectTextureDesc(resourceData, vxgiData.RenderScale,
                    AAAAVxgiCommon.ResourceNamePrefix + nameof(AAAAVoxelGlobalIlluminationData.IndirectSpecularTexture)
                )
            );

            passData.PackedGridBufferCount = packedBufferCount;
            passData.PackedGridBuffer = builder.WriteBuffer(vxgiData.PackedGridBuffer);

            float boundsSize = volumeComponent.BoundsSize.value;
            passData.ConstantBuffer = new AAAAVxgiConstantBuffer
            {
                _VxgiGridResolution = math.float4(vxgiData.GridSize, 1.0f / vxgiData.GridSize, boundsSize / vxgiData.GridSize, vxgiData.GridSize / boundsSize),
                _VxgiLevelCount = (uint) vxgiData.GridMipCount,
                _VxgiDiffuseOpacityFactor = volumeComponent.DiffuseOpacityFactor.value,
                _VxgiSpecularOpacityFactor = volumeComponent.SpecularOpacityFactor.value,
            };

            float3 forwardBias = math.float3(
                volumeComponent.BoundsForwardBias.value, volumeComponent.BoundsVerticalForwardBias.value, volumeComponent.BoundsForwardBias.value
            );
            CreateBounds(cameraData, vxgiData.GridSize, boundsSize, forwardBias, ref passData.ConstantBuffer);
        }

        private static unsafe void CreateBounds(AAAACameraData cameraData, int gridSize, float boundsSize, float3 forwardBias,
            ref AAAAVxgiConstantBuffer vxgiConstantBuffer)
        {
            Transform cameraTransform = cameraData.Camera.transform;
            float3 center = (float3) cameraTransform.position + cameraTransform.forward * forwardBias * boundsSize;

            fixed (float* pBoundsMinFloat = vxgiConstantBuffer._VxgiGridBoundsMin)
            {
                var pBoundsMin = (float4*) pBoundsMinFloat;

                for (int i = 0; i < vxgiConstantBuffer._VxgiLevelCount; ++i)
                {
                    float cellSize = boundsSize / gridSize * (1 << i);
                    float3 boundsMin = center - boundsSize * 0.5f;
                    boundsMin = math.floor(boundsMin / cellSize) * cellSize;

                    pBoundsMin[i] = math.float4(boundsMin, 0);
                }
            }
        }

        private static TextureDesc CreateFullscreenIndirectTextureDesc(AAAAResourceData resourceData, int renderScale, string name)
        {
            TextureDesc cameraScaledColorDesc = resourceData.CameraScaledColorDesc;
            return new TextureDesc(cameraScaledColorDesc)
            {
                width = math.max(1, cameraScaledColorDesc.width / renderScale),
                height = math.max(1, cameraScaledColorDesc.height / renderScale),
                name = name,
                format = GraphicsFormat.R16G16B16A16_SFloat,
                clearBuffer = false,
            };
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            ConstantBuffer.PushGlobal(context.cmd, data.ConstantBuffer, ShaderIDs.ConstantBufferID);

            const int writeOffset = 0;
            const int clearValue = 0;
            _rawBufferClear.DispatchClear(context.cmd, data.PackedGridBuffer, data.PackedGridBufferCount, writeOffset, clearValue);
        }

        public class PassData : PassDataBase
        {
            public AAAAVxgiConstantBuffer ConstantBuffer;
            public BufferHandle PackedGridBuffer;
            public int PackedGridBufferCount;
        }

        private static class ShaderIDs
        {
            public static readonly int ConstantBufferID = Shader.PropertyToID(nameof(AAAAVxgiConstantBuffer));
        }
    }

}