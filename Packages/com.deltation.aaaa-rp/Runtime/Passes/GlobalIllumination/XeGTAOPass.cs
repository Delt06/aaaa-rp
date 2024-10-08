using DELTation.AAAARP.Core;
using DELTation.AAAARP.Data;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.ShaderLibrary.ThirdParty.XeGTAO;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.GlobalIllumination
{
    public class XeGTAOPass : AAAARenderPass<XeGTAOPass.PassData>
    {
        private readonly ComputeShader _denoiseCS;
        private readonly ComputeShader _mainPassCS;
        private readonly ComputeShader _prefilterDepthsCS;

        public XeGTAOPass(AAAARenderPassEvent renderPassEvent, AAAARenderPipelineRuntimeShaders shaders) : base(renderPassEvent)
        {
            _prefilterDepthsCS = shaders.XeGtaoPrefilterDepthsCS;
            _mainPassCS = shaders.XeGtaoMainPassCS;
            _denoiseCS = shaders.XeGtaoDenoiseCS;
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

            {
                TextureDesc textureDesc = resourceData.CameraScaledColorDesc;
                textureDesc.clearBuffer = false;
                textureDesc.name = nameof(PassData.WorkingDepths);
                textureDesc.enableRandomWrite = true;
                textureDesc.colorFormat = GraphicsFormat.R32_SFloat;

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
                textureDesc.name = nameof(PassData.AOTerm);
                textureDesc.enableRandomWrite = true;
                textureDesc.colorFormat = passData.OutputBentNormals ? GraphicsFormat.R32_UInt : GraphicsFormat.R8_UInt;
                passData.AOTerm = builder.CreateTransientTexture(textureDesc);
                textureDesc.name = nameof(PassData.AOTermPong);
                passData.AOTermPong = builder.CreateTransientTexture(textureDesc);

                textureDesc.name = nameof(AAAALightingData.GTAOTerm);
                lightingData.GTAOTerm = renderingData.RenderGraph.CreateTexture(textureDesc);
                passData.FinalAOTerm = builder.WriteTexture(lightingData.GTAOTerm);

                textureDesc.colorFormat = GraphicsFormat.R8_UNorm;
                textureDesc.name = nameof(PassData.Edges);
                passData.Edges = builder.CreateTransientTexture(textureDesc);
            }

            passData.Resolution = math.int2(cameraData.ScaledWidth, cameraData.ScaledHeight);
            passData.Settings = XeGTAO.GTAOSettings.Default;
            passData.Settings.QualityLevel = (int) xeGtaoSettings.QualityLevel;
            passData.Settings.DenoisePasses = xeGtaoSettings.DenoisePasses;
            passData.Settings.FinalValuePower *= xeGtaoSettings.FinalValuePower;

            const bool rowMajor = false;
            const uint frameCounter = 0;

            // Unity view-space Z is negated.
            // Not sure why negative Y is necessary here
            var viewCorrectionMatrix = Matrix4x4.Scale(new Vector3(1, -1, -1));
            XeGTAO.GTAOSettings.GTAOUpdateConstants(ref passData.GTAOConstants, cameraData.ScaledWidth, cameraData.ScaledHeight, passData.Settings,
                cameraData.GetGPUProjectionMatrixJittered(true) * viewCorrectionMatrix, rowMajor, frameCounter
            );
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            {
                const int kernelIndex = 0;
                const int threadGroupSizeDim = 16;
                int threadGroupsX = AAAAMathUtils.AlignUp(data.Resolution.x, threadGroupSizeDim) / threadGroupSizeDim;
                int threadGroupsY = AAAAMathUtils.AlignUp(data.Resolution.y, threadGroupSizeDim) / threadGroupSizeDim;
                context.cmd.SetComputeTextureParam(_prefilterDepthsCS, kernelIndex, "g_srcRawDepth", data.SrcRawDepth);
                context.cmd.SetComputeTextureParam(_prefilterDepthsCS, kernelIndex, "g_outWorkingDepthMIP0", new RenderTargetIdentifier(data.WorkingDepths[0]));
                context.cmd.SetComputeTextureParam(_prefilterDepthsCS, kernelIndex, "g_outWorkingDepthMIP1", new RenderTargetIdentifier(data.WorkingDepths[1]));
                context.cmd.SetComputeTextureParam(_prefilterDepthsCS, kernelIndex, "g_outWorkingDepthMIP2", new RenderTargetIdentifier(data.WorkingDepths[2]));
                context.cmd.SetComputeTextureParam(_prefilterDepthsCS, kernelIndex, "g_outWorkingDepthMIP3", new RenderTargetIdentifier(data.WorkingDepths[3]));
                context.cmd.SetComputeTextureParam(_prefilterDepthsCS, kernelIndex, "g_outWorkingDepthMIP4", new RenderTargetIdentifier(data.WorkingDepths[4]));
                ConstantBuffer.Push(data.GTAOConstants, _prefilterDepthsCS, Shader.PropertyToID(nameof(XeGTAO.GTAOConstantsCS)));

                context.cmd.DispatchCompute(_prefilterDepthsCS, kernelIndex, threadGroupsX, threadGroupsY, 1);

                for (int mipIndex = 1; mipIndex < XeGTAO.XE_GTAO_DEPTH_MIP_LEVELS; mipIndex++)
                {
                    context.cmd.CopyTexture(data.WorkingDepths[mipIndex], 0, 0, data.WorkingDepths[0], 0, mipIndex);
                }
            }

            {
                CoreUtils.SetKeyword(_mainPassCS, "XE_GTAO_COMPUTE_BENT_NORMALS", data.OutputBentNormals);

                int kernelIndex = data.Settings.QualityLevel;
                int threadGroupsX = AAAAMathUtils.AlignUp(data.Resolution.x, XeGTAO.XE_GTAO_NUMTHREADS_X) / XeGTAO.XE_GTAO_NUMTHREADS_X;
                int threadGroupsY = AAAAMathUtils.AlignUp(data.Resolution.y, XeGTAO.XE_GTAO_NUMTHREADS_Y) / XeGTAO.XE_GTAO_NUMTHREADS_Y;

                context.cmd.SetComputeTextureParam(_mainPassCS, kernelIndex, "g_srcWorkingDepth", data.WorkingDepths[0]);
                context.cmd.SetComputeTextureParam(_mainPassCS, kernelIndex, "g_outWorkingAOTerm", data.AOTerm);
                context.cmd.SetComputeTextureParam(_mainPassCS, kernelIndex, "g_outWorkingEdges", data.Edges);
                ConstantBuffer.Push(data.GTAOConstants, _mainPassCS, Shader.PropertyToID(nameof(XeGTAO.GTAOConstantsCS)));

                context.cmd.DispatchCompute(_mainPassCS, kernelIndex, threadGroupsX, threadGroupsY, 1);
            }

            {
                CoreUtils.SetKeyword(_denoiseCS, "XE_GTAO_COMPUTE_BENT_NORMALS", data.OutputBentNormals);

                int passCount = math.max(1, data.Settings.DenoisePasses);
                for (int passIndex = 0; passIndex < passCount; passIndex++)
                {
                    bool isLastPass = passIndex == passCount - 1;
                    int kernelIndex = isLastPass ? 1 : 0;

                    int threadGroupsX = AAAAMathUtils.AlignUp(data.Resolution.x, XeGTAO.XE_GTAO_NUMTHREADS_X * 2) / XeGTAO.XE_GTAO_NUMTHREADS_X;
                    int threadGroupsY = AAAAMathUtils.AlignUp(data.Resolution.y, XeGTAO.XE_GTAO_NUMTHREADS_Y) / XeGTAO.XE_GTAO_NUMTHREADS_Y;

                    context.cmd.SetComputeTextureParam(_denoiseCS, kernelIndex, "g_srcWorkingAOTerm", data.AOTerm);
                    context.cmd.SetComputeTextureParam(_denoiseCS, kernelIndex, "g_srcWorkingEdges", data.Edges);
                    context.cmd.SetComputeTextureParam(_denoiseCS, kernelIndex, "g_outFinalAOTerm", isLastPass ? data.FinalAOTerm : data.AOTermPong);
                    ConstantBuffer.Push(data.GTAOConstants, _denoiseCS, Shader.PropertyToID(nameof(XeGTAO.GTAOConstantsCS)));

                    context.cmd.DispatchCompute(_denoiseCS, kernelIndex, threadGroupsX, threadGroupsY, 1);
                    (data.AOTerm, data.AOTermPong) = (data.AOTermPong, data.AOTerm);
                }
            }

            context.cmd.SetGlobalTexture("_GTAOTerm", data.FinalAOTerm);
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
            public XeGTAO.GTAOSettings Settings;
            public TextureHandle SrcRawDepth;
            public TextureHandle[] WorkingDepths = new TextureHandle[XeGTAO.XE_GTAO_DEPTH_MIP_LEVELS];
        }
    }
}