using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DELTation.AAAARP.Debugging
{
    public class AAAADebugStats
    {
        private const double ForgetTime = 2.0;

        public readonly Dictionary<Camera, GPUCullingStats> GPUCulling = new();

        public static double TimeNow => Time.timeSinceLevelLoadAsDouble;

        public void BuildGPUCullingString(StringBuilder stringBuilder)
        {
            bool any = false;

            foreach (KeyValuePair<Camera, GPUCullingStats> kvp in GPUCulling)
            {
                if (kvp.Key == null)
                {
                    continue;
                }

                if (TimeNow - kvp.Value.LastUpdateTime > ForgetTime)
                {
                    continue;
                }

                if (any)
                {
                    stringBuilder.Append("\n");
                }

                stringBuilder.Append(kvp.Key.name);
                stringBuilder.Append(":");

                BuildField(stringBuilder, nameof(AAAAGPUCullingDebugData.FrustumCulledInstances), kvp.Value.Data.FrustumCulledInstances);
                BuildField(stringBuilder, nameof(AAAAGPUCullingDebugData.FrustumCulledMeshlets), kvp.Value.Data.FrustumCulledMeshlets);

                BuildField(stringBuilder, nameof(AAAAGPUCullingDebugData.OcclusionCulledInstances), kvp.Value.Data.OcclusionCulledInstances);
                BuildField(stringBuilder, nameof(AAAAGPUCullingDebugData.OcclusionCulledMeshlets), kvp.Value.Data.OcclusionCulledMeshlets);

                BuildField(stringBuilder, nameof(AAAAGPUCullingDebugData.ConeCulledMeshlets), kvp.Value.Data.ConeCulledMeshlets);

                any = true;
            }

            return;

            static void BuildField(StringBuilder stringBuilder, string label, uint value)
            {
                stringBuilder.Append("\n\t");
                stringBuilder.Append(label);
                stringBuilder.Append(": ");
                stringBuilder.Append(value);
            }
        }

        public struct GPUCullingStats
        {
            public double LastUpdateTime;
            public AAAAGPUCullingDebugData Data;
        }
    }
}