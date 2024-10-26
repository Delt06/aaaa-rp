using System.Diagnostics.CodeAnalysis;
using DELTation.AAAARP.Core;
using DELTation.AAAARP.Data;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.RenderPipelineResources;
using DELTation.AAAARP.ShaderLibrary.ThirdParty.XeGTAO;
using DELTation.AAAARP.Volumes;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.GlobalIllumination.AO
{
    public class XeGTAOPass : AAAARenderPass<XeGTAOPass.PassData>
    {
        private readonly ComputeShader _denoiseCS;
        private readonly ComputeShader _mainPassCS;
        private readonly ComputeShader _prefilterDepthsCS;

        public XeGTAOPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent)
        {
            AAAAXeGtaoRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAAXeGtaoRuntimeShaders>();
            _prefilterDepthsCS = shaders.PrefilterDepthsCS;
            _mainPassCS = shaders.MainPassCS;
            _denoiseCS = shaders.DenoiseCS;
        }

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.Get<AAAARenderingData>();
            AAAAResourceData resourceData = frameData.Get<AAAAResourceData>();
            AAAACameraData cameraData = frameData.Get<AAAACameraData>();
            AAAALightingData lightingData = frameData.Get<AAAALightingData>();
            AAAALightingSettings.XeGTAOSettings xeGtaoSettings = renderingData.PipelineAsset.LightingSettings.GTAOSettings;

            passData.SrcRawDepth = builder.ReadTexture(resourceData.CameraScaledDepthBuffer);
            builder.ReadTexture(resourceData.GBufferNormals);

            int resolutionScale = (int) xeGtaoSettings.Resolution;
            int2 renderResolution = math.int2(cameraData.ScaledWidth, cameraData.ScaledHeight);
            passData.Resolution = math.max(1, renderResolution / resolutionScale);
            passData.ResolutionScale = math.float4((float2) passData.Resolution / renderResolution, 0, 0);

            {
                TextureDesc textureDesc = resourceData.CameraScaledColorDesc;
                textureDesc.clearBuffer = false;
                textureDesc.name = nameof(PassData.WorkingDepths);
                textureDesc.enableRandomWrite = true;
                textureDesc.format = GraphicsFormat.R32_SFloat;
                textureDesc.width = passData.Resolution.x;
                textureDesc.height = passData.Resolution.y;

                for (int mipIndex = 0; mipIndex < XeGTAO.XE_GTAO_DEPTH_MIP_LEVELS; mipIndex++)
                {
                    TextureDesc mipDesc = textureDesc;
                    if (mipIndex == 0)
                    {
                        mipDesc.useMipMap = true;
                    }
                    mipDesc.width >>= mipIndex;
                    mipDesc.height >>= mipIndex;
                    passData.WorkingDepths[mipIndex] = builder.CreateTransientTexture(mipDesc);
                }
            }

            passData.OutputBentNormals = xeGtaoSettings.BentNormals;

            {
                TextureDesc textureDesc = resourceData.CameraScaledColorDesc;
                textureDesc.clearBuffer = false;
                textureDesc.enableRandomWrite = true;
                textureDesc.format = passData.OutputBentNormals ? GraphicsFormat.R32_UInt : GraphicsFormat.R8_UInt;
                textureDesc.width = passData.Resolution.x;
                textureDesc.height = passData.Resolution.y;

                textureDesc.name = nameof(PassData.AOTerm);
                passData.AOTerm = builder.CreateTransientTexture(textureDesc);
                textureDesc.name = nameof(PassData.AOTermPong);
                passData.AOTermPong = builder.CreateTransientTexture(textureDesc);

                textureDesc.name = nameof(AAAALightingData.GTAOTerm);
                lightingData.GTAOTerm = renderingData.RenderGraph.CreateTexture(textureDesc);
                passData.FinalAOTerm = builder.WriteTexture(lightingData.GTAOTerm);

                textureDesc.format = GraphicsFormat.R8_UNorm;
                textureDesc.name = nameof(PassData.Edges);
                passData.Edges = builder.CreateTransientTexture(textureDesc);
            }

            AAAAGtaoVolumeComponent gtaoVolumeComponent = cameraData.VolumeStack.GetComponent<AAAAGtaoVolumeComponent>();
            passData.Settings = XeGTAO.GTAOSettings.Default;
            passData.Settings.QualityLevel = (int) xeGtaoSettings.QualityLevel;
            passData.Settings.DenoisePasses = (int) xeGtaoSettings.DenoisingLevel;
            passData.Settings.FinalValuePower *= gtaoVolumeComponent.FinalValuePower.value;
            passData.Settings.FalloffRange *= gtaoVolumeComponent.FalloffRange.value;

            const bool rowMajor = false;
            const uint frameCounter = 0;

            // Unity view-space Z is negated.
            // Not sure why negative Y is necessary here
            var viewCorrectionMatrix = Matrix4x4.Scale(new Vector3(1, -1, -1));
            XeGTAO.GTAOSettings.GTAOUpdateConstants(ref passData.GTAOConstants, passData.Resolution.x, passData.Resolution.y, passData.Settings,
                cameraData.GetGPUProjectionMatrixJittered(true) * viewCorrectionMatrix, rowMajor, frameCounter
            );
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            using (new ProfilingScope(context.cmd, Profiling.PrefilterDepths))
            {
                const int kernelIndex = 0;
                const int threadGroupSizeDim = 16;
                int threadGroupsX = AAAAMathUtils.AlignUp(data.Resolution.x, threadGroupSizeDim) / threadGroupSizeDim;
                int threadGroupsY = AAAAMathUtils.AlignUp(data.Resolution.y, threadGroupSizeDim) / threadGroupSizeDim;
                context.cmd.SetComputeTextureParam(_prefilterDepthsCS, kernelIndex, ShaderIDs.PrefilterDepths.g_srcRawDepth, data.SrcRawDepth);
                context.cmd.SetComputeTextureParam(_prefilterDepthsCS, kernelIndex, ShaderIDs.PrefilterDepths.g_outWorkingDepthMIP0, data.WorkingDepths[0]);
                context.cmd.SetComputeTextureParam(_prefilterDepthsCS, kernelIndex, ShaderIDs.PrefilterDepths.g_outWorkingDepthMIP1, data.WorkingDepths[1]);
                context.cmd.SetComputeTextureParam(_prefilterDepthsCS, kernelIndex, ShaderIDs.PrefilterDepths.g_outWorkingDepthMIP2, data.WorkingDepths[2]);
                context.cmd.SetComputeTextureParam(_prefilterDepthsCS, kernelIndex, ShaderIDs.PrefilterDepths.g_outWorkingDepthMIP3, data.WorkingDepths[3]);
                context.cmd.SetComputeTextureParam(_prefilterDepthsCS, kernelIndex, ShaderIDs.PrefilterDepths.g_outWorkingDepthMIP4, data.WorkingDepths[4]);
                ConstantBuffer.Push(data.GTAOConstants, _prefilterDepthsCS, Shader.PropertyToID(nameof(XeGTAO.GTAOConstantsCS)));

                context.cmd.DispatchCompute(_prefilterDepthsCS, kernelIndex, threadGroupsX, threadGroupsY, 1);

                for (int mipIndex = 1; mipIndex < XeGTAO.XE_GTAO_DEPTH_MIP_LEVELS; mipIndex++)
                {
                    context.cmd.CopyTexture(data.WorkingDepths[mipIndex], 0, 0, data.WorkingDepths[0], 0, mipIndex);
                }
            }

            using (new ProfilingScope(context.cmd, Profiling.MainPass))
            {
                CoreUtils.SetKeyword(_mainPassCS, Keywords.XE_GTAO_COMPUTE_BENT_NORMALS, data.OutputBentNormals);

                int kernelIndex = data.Settings.QualityLevel;
                int threadGroupsX = AAAAMathUtils.AlignUp(data.Resolution.x, XeGTAO.XE_GTAO_NUMTHREADS_X) / XeGTAO.XE_GTAO_NUMTHREADS_X;
                int threadGroupsY = AAAAMathUtils.AlignUp(data.Resolution.y, XeGTAO.XE_GTAO_NUMTHREADS_Y) / XeGTAO.XE_GTAO_NUMTHREADS_Y;

                context.cmd.SetComputeTextureParam(_mainPassCS, kernelIndex, ShaderIDs.MainPass.g_srcWorkingDepth, data.WorkingDepths[0]);
                context.cmd.SetComputeTextureParam(_mainPassCS, kernelIndex, ShaderIDs.MainPass.g_outWorkingAOTerm, data.AOTerm);
                context.cmd.SetComputeTextureParam(_mainPassCS, kernelIndex, ShaderIDs.MainPass.g_outWorkingEdges, data.Edges);
                ConstantBuffer.Push(data.GTAOConstants, _mainPassCS, Shader.PropertyToID(nameof(XeGTAO.GTAOConstantsCS)));

                context.cmd.DispatchCompute(_mainPassCS, kernelIndex, threadGroupsX, threadGroupsY, 1);
            }

            using (new ProfilingScope(context.cmd, Profiling.Denoise))
            {
                CoreUtils.SetKeyword(_denoiseCS, Keywords.XE_GTAO_COMPUTE_BENT_NORMALS, data.OutputBentNormals);

                int passCount = math.max(1, data.Settings.DenoisePasses);
                for (int passIndex = 0; passIndex < passCount; passIndex++)
                {
                    bool isLastPass = passIndex == passCount - 1;
                    int kernelIndex = isLastPass ? 1 : 0;

                    int threadGroupsX = AAAAMathUtils.AlignUp(data.Resolution.x, XeGTAO.XE_GTAO_NUMTHREADS_X * 2) / XeGTAO.XE_GTAO_NUMTHREADS_X;
                    int threadGroupsY = AAAAMathUtils.AlignUp(data.Resolution.y, XeGTAO.XE_GTAO_NUMTHREADS_Y) / XeGTAO.XE_GTAO_NUMTHREADS_Y;

                    context.cmd.SetComputeTextureParam(_denoiseCS, kernelIndex, ShaderIDs.Denoise.g_srcWorkingAOTerm, data.AOTerm);
                    context.cmd.SetComputeTextureParam(_denoiseCS, kernelIndex, ShaderIDs.Denoise.g_srcWorkingEdges, data.Edges);
                    context.cmd.SetComputeTextureParam(_denoiseCS, kernelIndex, ShaderIDs.Denoise.g_outFinalAOTerm,
                        isLastPass ? data.FinalAOTerm : data.AOTermPong
                    );
                    ConstantBuffer.Push(data.GTAOConstants, _denoiseCS, Shader.PropertyToID(nameof(XeGTAO.GTAOConstantsCS)));

                    context.cmd.DispatchCompute(_denoiseCS, kernelIndex, threadGroupsX, threadGroupsY, 1);
                    (data.AOTerm, data.AOTermPong) = (data.AOTermPong, data.AOTerm);
                }
            }

            context.cmd.SetGlobalTexture(ShaderIDs.Global._GTAOTerm, data.FinalAOTerm);
            context.cmd.SetGlobalVector(ShaderIDs.Global._GTAOResolutionScale, data.ResolutionScale);
        }

        private static class Profiling
        {
            public static ProfilingSampler PrefilterDepths = new(nameof(PrefilterDepths));
            public static ProfilingSampler MainPass = new(nameof(MainPass));
            public static ProfilingSampler Denoise = new(nameof(Denoise));
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class Keywords
        {
            public static readonly string XE_GTAO_COMPUTE_BENT_NORMALS = nameof(XE_GTAO_COMPUTE_BENT_NORMALS);
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class ShaderIDs
        {
            public static class Global
            {
                public static readonly int _GTAOTerm = Shader.PropertyToID(nameof(_GTAOTerm));
                public static readonly int _GTAOResolutionScale = Shader.PropertyToID(nameof(_GTAOResolutionScale));
            }

            public static class PrefilterDepths
            {
                public static readonly int g_srcRawDepth = Shader.PropertyToID(nameof(g_srcRawDepth));
                public static readonly int g_outWorkingDepthMIP0 = Shader.PropertyToID(nameof(g_outWorkingDepthMIP0));
                public static readonly int g_outWorkingDepthMIP1 = Shader.PropertyToID(nameof(g_outWorkingDepthMIP1));
                public static readonly int g_outWorkingDepthMIP2 = Shader.PropertyToID(nameof(g_outWorkingDepthMIP2));
                public static readonly int g_outWorkingDepthMIP3 = Shader.PropertyToID(nameof(g_outWorkingDepthMIP3));
                public static readonly int g_outWorkingDepthMIP4 = Shader.PropertyToID(nameof(g_outWorkingDepthMIP4));
            }

            public static class MainPass
            {
                public static readonly int g_srcWorkingDepth = Shader.PropertyToID(nameof(g_srcWorkingDepth));
                public static readonly int g_outWorkingAOTerm = Shader.PropertyToID(nameof(g_outWorkingAOTerm));
                public static readonly int g_outWorkingEdges = Shader.PropertyToID(nameof(g_outWorkingEdges));
            }

            public static class Denoise
            {
                public static readonly int g_srcWorkingAOTerm = Shader.PropertyToID(nameof(g_srcWorkingAOTerm));
                public static readonly int g_srcWorkingEdges = Shader.PropertyToID(nameof(g_srcWorkingEdges));
                public static readonly int g_outFinalAOTerm = Shader.PropertyToID(nameof(g_outFinalAOTerm));
            }
        }

        public class PassData : PassDataBase
        {
            public TextureHandle AOTerm;
            public TextureHandle AOTermPong;
            public TextureHandle Edges;
            public TextureHandle FinalAOTerm;
            public XeGTAO.GTAOConstantsCS GTAOConstants;
            public bool OutputBentNormals;
            public int2 Resolution;
            public float4 ResolutionScale;
            public XeGTAO.GTAOSettings Settings;
            public TextureHandle SrcRawDepth;
            public TextureHandle[] WorkingDepths = new TextureHandle[XeGTAO.XE_GTAO_DEPTH_MIP_LEVELS];
        }
    }
}