using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.IBL
{
    public class PreFilterEnvironmentPass : AAAARenderPass<PreFilterEnvironmentPass.PassData>, IDisposable
    {
        private const string TempSideTextureName = nameof(AAAAImageBasedLightingData.PreFilteredEnvironmentMap) + "_TempSide";

        private readonly Material _material;
        private readonly MaterialPropertyBlock _propertyBlock = new();

        public PreFilterEnvironmentPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders runtimeShaders) : base(renderPassEvent) =>
            _material = CoreUtils.CreateEngineMaterial(runtimeShaders.PreFilterEnvironmentPS);

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
        }

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAAImageBasedLightingData imageBasedLightingData = frameData.Get<AAAAImageBasedLightingData>();

            if (!imageBasedLightingData.PreFilteredEnvironmentMapIsDirty)
            {
                passData.CullPass = true;
                return;
            }

            passData.CullPass = false;
            imageBasedLightingData.PreFilteredEnvironmentMapIsDirty = false;

            passData.Source = ReflectionProbe.defaultTexture;
            passData.SourceHDRDecodeValues = ReflectionProbe.defaultTextureHDRDecodeValues;

            passData.Material = _material;

            passData.FinalDestination = builder.WriteTexture(imageBasedLightingData.PreFilteredEnvironmentMap);

            RenderTextureDescriptor destinationDesc = imageBasedLightingData.PreFilteredEnvironmentMapDesc;
            passData.MipCount = destinationDesc.mipCount;


            passData.TempSides.Clear();

            for (int mipIndex = 0; mipIndex < destinationDesc.mipCount; ++mipIndex)
            {
                var sideDescriptor = new RenderTextureDescriptor(
                    destinationDesc.width >> mipIndex, destinationDesc.height >> mipIndex,
                    destinationDesc.graphicsFormat, destinationDesc.depthBufferBits
                );

                for (int sideIndex = 0; sideIndex < CubemapUtils.SideCount; sideIndex++)
                {
                    passData.TempSides.Add(builder.CreateTransientTexture(new TextureDesc(sideDescriptor)
                            {
                                clearBuffer = false,
                                name = TempSideTextureName,
                            }
                        )
                    );
                }
            }
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            if (data.CullPass)
            {
                return;
            }

            using (new ProfilingScope(context.cmd, Profiling.PreFilter))
            {
                ReadOnlySpan<CubemapUtils.SideOrientation> sideOrientations = CubemapUtils.GetSideOrientations();

                _propertyBlock.SetTexture(ShaderIDs._Source, data.Source);
                _propertyBlock.SetVector(ShaderIDs._SourceHDRDecodeValues, data.SourceHDRDecodeValues);
                _propertyBlock.SetVector(ShaderIDs._SourceResolution, new Vector4(data.Source.width, data.Source.height));

                for (int mipIndex = 0; mipIndex < data.MipCount; mipIndex++)
                {
                    float roughness = (float) mipIndex / (data.MipCount - 1);
                    _propertyBlock.SetFloat(ShaderIDs._Roughness, roughness);

                    for (int sideIndex = 0; sideIndex < CubemapUtils.SideCount; sideIndex++)
                    {
                        ref readonly CubemapUtils.SideOrientation sideOrientation = ref sideOrientations[sideIndex];

                        context.cmd.SetRenderTarget(data.TempSides[mipIndex * CubemapUtils.SideCount + sideIndex]);

                        _propertyBlock.SetVector(ShaderIDs._Forward, sideOrientation.Forward);
                        _propertyBlock.SetVector(ShaderIDs._Up, sideOrientation.Up);

                        Material material = data.Material;
                        const int shaderPass = 0;
                        context.cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Triangles, 3, 1, _propertyBlock);
                    }
                }
            }

            using (new ProfilingScope(context.cmd, Profiling.CopyToCubemap))
            {
                for (int mipIndex = 0; mipIndex < data.MipCount; mipIndex++)
                {
                    float roughness = (float) mipIndex / (mipIndex - 1);
                    _propertyBlock.SetFloat(ShaderIDs._Roughness, roughness);

                    for (int sideIndex = 0; sideIndex < CubemapUtils.SideCount; sideIndex++)
                    {
                        TextureHandle tempSide = data.TempSides[mipIndex * CubemapUtils.SideCount + sideIndex];
                        context.cmd.CopyTexture(tempSide, 0, 0, data.FinalDestination, sideIndex, mipIndex);
                    }
                }
            }
        }

        private static class Profiling
        {
            public static readonly ProfilingSampler PreFilter = new(nameof(PreFilter));
            public static readonly ProfilingSampler CopyToCubemap = new(nameof(CopyToCubemap));
        }

        public class PassData : PassDataBase
        {
            public readonly List<TextureHandle> TempSides = new();

            public bool CullPass;
            public TextureHandle FinalDestination;
            public Material Material;
            public int MipCount;
            public Texture Source;
            public Vector4 SourceHDRDecodeValues;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static readonly int _Source = Shader.PropertyToID(nameof(_Source));
            public static readonly int _SourceHDRDecodeValues = Shader.PropertyToID(nameof(_SourceHDRDecodeValues));
            public static readonly int _SourceResolution = Shader.PropertyToID(nameof(_SourceResolution));

            public static readonly int _Roughness = Shader.PropertyToID(nameof(_Roughness));
            public static readonly int _Forward = Shader.PropertyToID(nameof(_Forward));
            public static readonly int _Up = Shader.PropertyToID(nameof(_Up));
        }
    }
}