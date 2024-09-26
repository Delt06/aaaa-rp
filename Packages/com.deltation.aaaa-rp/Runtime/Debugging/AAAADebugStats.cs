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

                stringBuilder.Append("\n\t");
                stringBuilder.Append(nameof(AAAAGPUCullingDebugData.OcclusionCulledInstances));
                stringBuilder.Append(":\t");
                stringBuilder.Append(kvp.Value.Data.OcclusionCulledInstances);

                stringBuilder.Append("\n\t");
                stringBuilder.Append(nameof(AAAAGPUCullingDebugData.OcclusionCulledMeshlets));
                stringBuilder.Append(":\t");
                stringBuilder.Append(kvp.Value.Data.OcclusionCulledMeshlets);
                any = true;
            }
        }

        public struct GPUCullingStats
        {
            public double LastUpdateTime;
            public AAAAGPUCullingDebugData Data;
        }
    }
}