using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes
{
    public class PassDataBase { }

    public abstract class AAAARenderPassBase : IRenderGraphRecorder
    {
        protected AAAARenderPassBase(AAAARenderPassEvent renderPassEvent)
        {
            RenderPassEvent = renderPassEvent;

            // ReSharper disable once VirtualMemberCallInConstructor
            ProfilingSampler = new ProfilingSampler(Name);
        }

        public abstract string Name { get; }

        public AAAARenderPassEvent RenderPassEvent { get; set; }

        public ProfilingSampler ProfilingSampler { get; }

        public abstract void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData);

        public static bool operator <(AAAARenderPassBase lhs, AAAARenderPassBase rhs) => lhs.RenderPassEvent < rhs.RenderPassEvent;

        public static bool operator >(AAAARenderPassBase lhs, AAAARenderPassBase rhs) => lhs.RenderPassEvent > rhs.RenderPassEvent;
    }

    public abstract class AAAARenderPass<T> : AAAARenderPassBase where T : PassDataBase, new()
    {
        private readonly BaseRenderFunc<T, RenderGraphContext> _renderFunc;

        protected AAAARenderPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent) => _renderFunc = Render;

        public sealed override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using RenderGraphBuilder builder = renderGraph.AddRenderPass(Name, out T passData);

            Setup(builder, passData, frameData);
            builder.SetRenderFunc(_renderFunc);
        }

        protected abstract void Setup(RenderGraphBuilder builder, T passData, ContextContainer frameData);

        protected abstract void Render(T data, RenderGraphContext context);
    }

    public abstract class AAAARasterRenderPass<T> : AAAARenderPassBase where T : PassDataBase, new()
    {
        private readonly BaseRenderFunc<T, RasterGraphContext> _renderFunc;

        protected AAAARasterRenderPass(AAAARenderPassEvent renderPassEvent) : base(renderPassEvent) => _renderFunc = Render;

        public sealed override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(Name, out T passData);

            Setup(builder, passData, frameData);
            builder.SetRenderFunc(_renderFunc);
        }

        protected abstract void Setup(IRasterRenderGraphBuilder builder, T passData, ContextContainer frameData);

        protected abstract void Render(T data, RasterGraphContext context);
    }

}