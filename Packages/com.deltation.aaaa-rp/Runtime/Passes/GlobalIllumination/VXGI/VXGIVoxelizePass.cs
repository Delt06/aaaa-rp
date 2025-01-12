using System;
using DELTation.AAAARP.FrameData;
using DELTation.AAAARP.Renderers;
using DELTation.AAAARP.RenderPipelineResources;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes.GlobalIllumination.VXGI
{
    /// <summary>
    /// Sources:
    /// - https://wickedengine.net/2017/08/voxel-based-global-illumination/
    /// - https://github.com/turanszkij/WickedEngine/blob/97e08abfe5f1f086a845e353c832583c89f3edd3/WickedEngine/wiRenderer.cpp#L9186
    /// </summary>
    public class VXGIVoxelizePass : AAAARenderPass<VXGIVoxelizePass.PassData>, IDisposable
    {
        private readonly AAAARendererContainer.RendererList[] _rendererLists;

        public VXGIVoxelizePass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent)
        {
            AAAAVxgiRuntimeShaders shaders = GraphicsSettings.GetRenderPipelineSettings<AAAAVxgiRuntimeShaders>();

            _rendererLists = new AAAARendererContainer.RendererList[(int) AAAARendererListID.Count];

            for (int listID = 0; listID < _rendererLists.Length; listID++)
            {
                _rendererLists[listID] = AAAARendererContainer.CreateRendererList((AAAARendererListID) listID, shaders.VoxelizePS);
            }
        }

        public override string Name => "VXGI.Voxelize";

        public void Dispose()
        {
            foreach (AAAARendererContainer.RendererList rendererList in _rendererLists)
            {
                rendererList.Dispose();
            }
        }

        protected override void Setup(RenderGraphBuilder builder, PassData passData, ContextContainer frameData)
        {
            AAAARenderingData renderingData = frameData.GetOrCreate<AAAARenderingData>();
            AAAAVoxelGlobalIlluminationData vxgiData = frameData.GetOrCreate<AAAAVoxelGlobalIlluminationData>();

            passData.RendererContainer = renderingData.RendererContainer;
            passData.PackedGridBuffer = builder.WriteBuffer(vxgiData.PackedGridBuffer);
            passData.DummyRT = builder.CreateTransientTexture(new TextureDesc(vxgiData.GridSize, vxgiData.GridSize)
                {
                    name = nameof(VXGIVoxelizePass) + "_" + nameof(PassData.DummyRT),
                    format = GraphicsFormat.R8_UNorm,
                }
            );
        }

        protected override void Render(PassData data, RenderGraphContext context)
        {
            context.cmd.SetRenderTarget(data.DummyRT);
            context.cmd.SetRandomWriteTarget(1, data.PackedGridBuffer);
            data.RendererContainer.Draw(CameraType.Game, context.cmd, AAAARendererContainer.PassType.Default, overrideRendererLists: _rendererLists);
            context.cmd.ClearRandomWriteTargets();
        }

        public class PassData : PassDataBase
        {
            public TextureHandle DummyRT;
            public BufferHandle PackedGridBuffer;
            public AAAARendererContainer RendererContainer;
        }
    }
}