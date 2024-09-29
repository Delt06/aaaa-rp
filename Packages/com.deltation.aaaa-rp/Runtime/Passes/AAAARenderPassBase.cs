using System;
using DELTation.AAAARP.Utils;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DELTation.AAAARP.Passes
{
    public class PassDataBase { }

    public abstract class AAAARenderPassBase : IRenderGraphRecorder
    {
        private readonly Lazy<ProfilingSampler> _profilingSampler;

        protected AAAARenderPassBase(AAAARenderPassEvent renderPassEvent)
        {
            RenderPassEvent = renderPassEvent;
            AutoName = PassUtils.CreateAutoName(GetType());
            _profilingSampler = new Lazy<ProfilingSampler>(() => new ProfilingSampler(Name));
        }

        protected string AutoName { get; }

        public virtual string Name => AutoName;

        public AAAARenderPassEvent RenderPassEvent { get; }

        public ProfilingSampler ProfilingSampler => _profilingSampler.Value;

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