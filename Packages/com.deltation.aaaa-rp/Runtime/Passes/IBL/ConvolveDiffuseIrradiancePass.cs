using System;
using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.IBL
{
    public class ConvolveDiffuseIrradiancePass : AAAARenderPass<ConvolveDiffuseIrradiancePass.PassData>, IDisposable
    {
        private readonly Material _material;
        private readonly MaterialPropertyBlock _propertyBlock = new();

        public ConvolveDiffuseIrradiancePass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders runtimeShaders) : base(renderPassEvent) =>
            _material = CoreUtils.CreateEngineMaterial(runtimeShaders.ConvolveDiffuseIrradiancePS);

        public override string Name => "ConvolveDiffuseIrradiance";

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
        }

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAAImageBasedLightingData imageBasedLightingData = frameData.Get<AAAAImageBasedLightingData>();

            SphericalHarmonicsL2 ambientProbe = RenderSettings.ambientProbe;
            if (imageBasedLightingData.DiffuseIrradianceAmbientProbe == ambientProbe)
            {
                passData.CullPass = true;
                return;
            }

            passData.CullPass = false;
            imageBasedLightingData.DiffuseIrradianceAmbientProbe = ambientProbe;

            passData.Source = ReflectionProbe.defaultTexture;
            passData.SourceHDRDecodeValues = ReflectionProbe.defaultTextureHDRDecodeValues;

            passData.Material = _material;

            passData.FinalDestination = builder.WriteTexture(imageBasedLightingData.DiffuseIrradiance);

            RenderTextureDescriptor diffuseIrradianceDesc = imageBasedLightingData.DiffuseIrradianceDesc;

            var sideDescriptor = new RenderTextureDescriptor(
                diffuseIrradianceDesc.width, diffuseIrradianceDesc.height,
                diffuseIrradianceDesc.graphicsFormat, diffuseIrradianceDesc.depthBufferBits
            );

            for (int index = 0; index < passData.TempSides.Length; index++)
            {
                passData.TempSides[index] = builder.CreateTransientTexture(new TextureDesc(sideDescriptor)
                    {
                        clearBuffer = false,
                        name = "DiffuseIrradianceCubemapSide",
                    }
                );
            }
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            if (data.CullPass)
            {
                return;
            }

            using (new ProfilingScope(Profiling.Convolve))
            {
                ReadOnlySpan<CubemapUtils.SideOrientation> sideOrientations = CubemapUtils.GetSideOrientations();

                _propertyBlock.SetTexture(ShaderIDs._Source, data.Source);
                _propertyBlock.SetVector(ShaderIDs._SourceHDRDecodeValues, data.SourceHDRDecodeValues);

                for (int sideIndex = 0; sideIndex < CubemapUtils.SideCount; sideIndex++)
                {
                    ref readonly CubemapUtils.SideOrientation sideOrientation = ref sideOrientations[sideIndex];

                    context.cmd.SetRenderTarget(data.TempSides[sideIndex]);

                    _propertyBlock.SetVector(ShaderIDs._Forward, sideOrientation.Forward);
                    _propertyBlock.SetVector(ShaderIDs._Up, sideOrientation.Up);

                    Material material = data.Material;
                    const int shaderPass = 0;
                    context.cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Triangles, 3, 1, _propertyBlock);
                }
            }

            using (new ProfilingScope(Profiling.CopyToCubemap))
            {
                for (int sideIndex = 0; sideIndex < CubemapUtils.SideCount; sideIndex++)
                {
                    context.cmd.CopyTexture(data.TempSides[sideIndex], 0, 0, data.FinalDestination, sideIndex, 0);
                }
            }
        }

        private static class Profiling
        {
            public static readonly ProfilingSampler Convolve = new(nameof(Convolve));
            public static readonly ProfilingSampler CopyToCubemap = new(nameof(CopyToCubemap));
        }

        public class PassData : PassDataBase
        {
            public readonly TextureHandle[] TempSides = new TextureHandle[CubemapUtils.SideCount];

            public bool CullPass;
            public TextureHandle FinalDestination;
            public Material Material;
            public Texture Source;
            public Vector4 SourceHDRDecodeValues;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static readonly int _Source = Shader.PropertyToID(nameof(_Source));
            public static readonly int _SourceHDRDecodeValues = Shader.PropertyToID(nameof(_SourceHDRDecodeValues));

            public static readonly int _Forward = Shader.PropertyToID(nameof(_Forward));
            public static readonly int _Up = Shader.PropertyToID(nameof(_Up));
        }
    }
}