using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using DELTation.AAAARP.Volumes;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using static DELTation.AAAARP.Lighting.AAAALPVCommon;

namespace DELTation.AAAARP.Passes.GlobalIllumination.LPV
{
    public class LPVInjectPass : AAAARenderPass<LPVInjectPass.PassData>, IDisposable
    {
        private readonly Material _injectBlockingPotentialMaterial;
        private readonly Material _injectMaterial;

        public LPVInjectPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders shaders) : base(renderPassEvent)
        {
            Shader shader = shaders.LpvInjectPS;
            _injectMaterial = CoreUtils.CreateEngineMaterial(shader);
            _injectBlockingPotentialMaterial = CoreUtils.CreateEngineMaterial(shader);
            CoreUtils.SetKeyword(_injectBlockingPotentialMaterial, ShaderKeywords.BLOCKING_POTENTIAL, true);
        }

        public override string Name => "LPV.Inject";

        public void Dispose()
        {
            CoreUtils.Destroy(_injectMaterial);
            CoreUtils.Destroy(_injectBlockingPotentialMaterial);
        }

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAALightPropagationVolumesData lpvData = frameData.Get<AAAALightPropagationVolumesData>();

            ref readonly AAAALightPropagationVolumesData.GridTextureSet gridBuffers = ref lpvData.PackedGridTextures;
            builder.ReadWriteTexture(passData.GridRedSH = gridBuffers.RedSH);
            builder.ReadWriteTexture(passData.GridGreenSH = gridBuffers.GreenSH);
            builder.ReadWriteTexture(passData.GridBlueSH = gridBuffers.BlueSH);
            passData.TempDepth = builder.CreateTransientTexture(new TextureDesc(gridBuffers.SHDesc)
                {
                    format = GraphicsFormat.D16_UNorm,
                    name = nameof(LPVInjectPass) + "_" + nameof(PassData.TempDepth),
                }
            );

            passData.BlockingPotential = lpvData.BlockingPotential;
            if (passData.BlockingPotential)
            {
                builder.ReadWriteTexture(passData.GridBlockingPotentialSH = gridBuffers.BlockingPotentialSH);
            }
            else
            {
                passData.GridBlockingPotentialSH = default;
            }

            AAAALPVVolumeComponent volumeComponent = cameraData.VolumeStack.GetComponent<AAAALPVVolumeComponent>();
            passData.Intensity = volumeComponent.Intensity.value;
            passData.Biases = new Vector4(volumeComponent.InjectionDepthBias.value, volumeComponent.InjectionNormalBias.value);
            passData.Batches.Clear();

            ref readonly AAAARenderTexturePoolSet rtPoolSet = ref renderingData.RtPoolSet;

            for (int index = 0; index < lpvData.Lights.Length; index++)
            {
                ref readonly RsmLight rsmLight = ref lpvData.Lights.ElementAtRefReadonly(index);
                ref readonly RsmAttachmentAllocation rsmAllocation = ref rsmLight.InjectedAllocation;
                int resolution = rsmAllocation.PositionsMap.Resolution;
                passData.Batches.Add(new PassData.Batch
                    {
                        RsmPositionMap = rtPoolSet.RsmPositionMap.LookupRenderTexture(rsmAllocation.PositionsMap),
                        RsmNormalMap = rtPoolSet.RsmNormalMap.LookupRenderTexture(rsmAllocation.NormalMap),
                        RsmFluxMap = rtPoolSet.RsmFluxMap.LookupRenderTexture(rsmAllocation.FluxMap),
                        LightDirectionWS = rsmLight.DirectionWS,
                        LightColor = rsmLight.Color,
                        RsmResolution = math.float4(resolution, resolution, math.float2(math.rcp(resolution))),
                    }
                );
            }

            builder.ReadTexture(resourceData.CameraScaledDepthBuffer);
            builder.ReadTexture(resourceData.GBufferNormals);
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            data.PropertyBlock.Clear();
            data.PropertyBlock.SetVector(ShaderIDs._Biases, data.Biases);

            data.RTs[0] = data.GridRedSH;
            data.RTs[1] = data.GridGreenSH;
            data.RTs[2] = data.GridBlueSH;
            context.cmd.SetRenderTarget(data.RTs, data.TempDepth);

            foreach (PassData.Batch batch in data.Batches)
            {
                data.PropertyBlock.SetVector(ShaderIDs._LightDirectionWS, batch.LightDirectionWS);
                data.PropertyBlock.SetVector(ShaderIDs._LightColor, batch.LightColor * data.Intensity);
                data.PropertyBlock.SetVector(ShaderIDs._RSMResolution, batch.RsmResolution);
                data.PropertyBlock.SetTexture(ShaderIDs._RSMPositionMap, batch.RsmPositionMap);
                data.PropertyBlock.SetTexture(ShaderIDs._RSMNormalMap, batch.RsmNormalMap);
                data.PropertyBlock.SetTexture(ShaderIDs._RSMFluxMap, batch.RsmFluxMap);
                DrawInjectionPass(context, _injectMaterial, batch, data.PropertyBlock);
            }

            if (data.BlockingPotential)
            {
                context.cmd.SetRenderTarget(data.GridBlockingPotentialSH);

                foreach (PassData.Batch batch in data.Batches)
                {
                    data.PropertyBlock.SetVector(ShaderIDs._LightDirectionWS, batch.LightDirectionWS);
                    data.PropertyBlock.SetVector(ShaderIDs._LightColor, batch.LightColor * data.Intensity);
                    data.PropertyBlock.SetVector(ShaderIDs._RSMResolution, batch.RsmResolution);
                    data.PropertyBlock.SetTexture(ShaderIDs._RSMPositionMap, batch.RsmPositionMap);
                    data.PropertyBlock.SetTexture(ShaderIDs._RSMNormalMap, batch.RsmNormalMap);
                    data.PropertyBlock.SetTexture(ShaderIDs._RSMFluxMap, batch.RsmFluxMap);
                    DrawInjectionPass(context, _injectBlockingPotentialMaterial, batch, data.PropertyBlock);
                }
            }
        }

        private static void DrawInjectionPass(in RenderGraphContext context, Material material, in PassData.Batch batch, MaterialPropertyBlock propertyBlock)
        {
            const int shaderPass = 0;
            const int instanceCount = 1;
            context.cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Points,
                (int) (batch.RsmResolution.x * batch.RsmResolution.y), instanceCount,
                propertyBlock
            );
        }

        public class PassData : PassDataBase
        {
            public readonly List<Batch> Batches = new();
            public readonly MaterialPropertyBlock PropertyBlock = new();
            public readonly RenderTargetIdentifier[] RTs = new RenderTargetIdentifier[3];
            public Vector4 Biases;
            public bool BlockingPotential;
            public TextureHandle GridBlockingPotentialSH;
            public TextureHandle GridBlueSH;
            public TextureHandle GridGreenSH;
            public TextureHandle GridRedSH;
            public float Intensity;
            public TextureHandle TempDepth;

            public struct Batch
            {
                public Texture RsmPositionMap;
                public Texture RsmNormalMap;
                public Texture RsmFluxMap;
                public float4 LightDirectionWS;
                public float4 RsmResolution;
                public float4 LightColor;
            }
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static readonly int _Biases = Shader.PropertyToID(nameof(_Biases));
            public static readonly int _LightDirectionWS = Shader.PropertyToID(nameof(_LightDirectionWS));
            public static readonly int _LightColor = Shader.PropertyToID(nameof(_LightColor));
            public static readonly int _RSMResolution = Shader.PropertyToID(nameof(_RSMResolution));
            public static readonly int _RSMPositionMap = Shader.PropertyToID(nameof(_RSMPositionMap));
            public static readonly int _RSMNormalMap = Shader.PropertyToID(nameof(_RSMNormalMap));
            public static readonly int _RSMFluxMap = Shader.PropertyToID(nameof(_RSMFluxMap));
        }
    }
}