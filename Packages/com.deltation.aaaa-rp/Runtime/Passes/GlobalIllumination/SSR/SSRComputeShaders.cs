using UnityEngine.Rendering;

namespace DELTation.AAAARP.Passes.GlobalIllumination.SSR
{
    [GenerateHLSL(PackingRules.Exact, needAccessors = false)]
    public static class SSRComputeShaders
    {
        public const int TraceThreadGroupSize = 32;
    }
}