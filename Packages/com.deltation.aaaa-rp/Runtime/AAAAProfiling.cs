using UnityEngine.Rendering;

namespace DELTation.AAAARP
{
    internal static class AAAAProfiling
    {
        public static class Pipeline
        {
            public static class Context
            {
                private const string Name = nameof(ScriptableRenderContext);

                public static readonly ProfilingSampler Submit = new($"{Name}.{nameof(ScriptableRenderContext.Submit)}");
            }
        }
    }

}