#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.AAAARP
{
    public partial class AAAARendererBase
    {
        partial void RenderGizmos(ScriptableRenderContext context, Camera camera)
        {
#if UNITY_EDITOR
            if (Handles.ShouldRenderGizmos())
            {
                CommandBuffer cmd = CommandBufferPool.Get();

                using (new ProfilingScope(cmd, Profiling.RenderGizmos))
                {
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
                    context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
#endif
        }

        private static partial class Profiling
        {
            public static readonly ProfilingSampler RenderGizmos = new($"{Name}.{nameof(RenderGizmos)}");
        }
    }
}
#endif