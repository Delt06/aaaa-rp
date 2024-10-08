#define XE_GTAO_USE_DEFAULT_CONSTANTS

using System;
using System.Diagnostics.CodeAnalysis;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using static Unity.Mathematics.math;
using Vector2i = Unity.Mathematics.int2;
using Vector2 = Unity.Mathematics.float2;

namespace DELTation.AAAARP.ShaderLibrary.ThirdParty.XeGTAO
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static class XeGTAO
    {
        // Global consts that need to be visible from both shader and cpp side
        public const int XE_GTAO_DEPTH_MIP_LEVELS = 5; // this one is hard-coded to 5 for now
        public const int XE_GTAO_NUMTHREADS_X = 8; // these can be changed
        public const int XE_GTAO_NUMTHREADS_Y = 8; // these can be changed

        // some constants reduce performance if provided as dynamic values; if these constants are not required to be dynamic and they match default values, 
        // set XE_GTAO_USE_DEFAULT_CONSTANTS and the code will compile into a more efficient shader
        public const float
            XE_GTAO_DEFAULT_RADIUS_MULTIPLIER =
                1.457f; // allows us to use different value as compared to ground truth radius to counter inherent screen space biases
        public const float XE_GTAO_DEFAULT_FALLOFF_RANGE = 0.615f; // distant samples contribute less
        public const float XE_GTAO_DEFAULT_SAMPLE_DISTRIBUTION_POWER = 2.0f; // small crevices more important than big surfaces
        public const float XE_GTAO_DEFAULT_THIN_OCCLUDER_COMPENSATION = 0.0f; // the new 'thickness heuristic' approach
        public const float
            XE_GTAO_DEFAULT_FINAL_VALUE_POWER =
                2.2f; // modifies the final ambient occlusion value using power function - this allows some of the above heuristics to do different things
        public const float
            XE_GTAO_DEFAULT_DEPTH_MIP_SAMPLING_OFFSET =
                3.30f; // main trade-off between performance (memory bandwidth) and quality (temporal stability is the first affected, thin objects next)

        public const float
            XE_GTAO_OCCLUSION_TERM_SCALE =
                1.5f; // for packing in UNORM (because raw, pre-denoised occlusion term can overshoot 1 but will later average out to 1)

        public const uint XE_HILBERT_LEVEL = 6U;
        public const uint XE_HILBERT_WIDTH = 1U << (int) XE_HILBERT_LEVEL;
        public const uint XE_HILBERT_AREA = XE_HILBERT_WIDTH * XE_HILBERT_WIDTH;

        private static uint HilbertIndex(uint posX, uint posY)
        {
            uint index = 0U;
            for (uint curLevel = XE_HILBERT_WIDTH / 2U; curLevel > 0U; curLevel /= 2U)
            {
                uint regionX = (posX & curLevel) > 0U ? 1U : 0U;
                uint regionY = (posY & curLevel) > 0U ? 1U : 0U;
                index += curLevel * curLevel * (3U * regionX ^ regionY);
                if (regionY == 0U)
                {
                    if (regionX == 1U)
                    {
                        posX = XE_HILBERT_WIDTH - 1U - posX;
                        posY = XE_HILBERT_WIDTH - 1U - posY;
                    }

                    (posX, posY) = (posY, posX);
                }
            }
            return index;
        }

        [GenerateHLSL(PackingRules.Exact, needAccessors = false, generateCBuffer = true)]
        public struct GTAOConstantsCS
        {
            public Vector2i ViewportSize;
            public Vector2 ViewportPixelSize; // .zw == 1.0 / ViewportSize.xy

            public Vector2 DepthUnpackConsts;
            public Vector2 CameraTanHalfFOV;

            public Vector2 NDCToViewMul;
            public Vector2 NDCToViewAdd;

            public Vector2 NDCToViewMul_x_PixelSize;
            public float EffectRadius; // world (viewspace) maximum size of the shadow
            public float EffectFalloffRange;

            public float RadiusMultiplier;
            public float Padding0;
            public float FinalValuePower;
            public float DenoiseBlurBeta;

            public float SampleDistributionPower;
            public float ThinOccluderCompensation;
            public float DepthMIPSamplingOffset;
            public int NoiseIndex; // frameIndex % 64 if using TAA or 0 otherwise
        }

        [Serializable]
        public struct GTAOSettings
        {
            public int QualityLevel; // 0: low; 1: medium; 2: high; 3: ultra
            public int DenoisePasses; // 0: disabled; 1: sharp; 2: medium; 3: soft
            public float Radius; // [0.0,  ~ ]   World (view) space size of the occlusion sphere.

            // auto-tune-d settings
            public float RadiusMultiplier;
            public float FalloffRange;
            public float SampleDistributionPower;
            public float ThinOccluderCompensation;
            public float FinalValuePower;
            public float DepthMIPSamplingOffset;

            public static GTAOSettings Default => new()
            {
                QualityLevel = 2,
                DenoisePasses = 1,
                Radius = 0.5f,
                RadiusMultiplier = XE_GTAO_DEFAULT_RADIUS_MULTIPLIER,
                FalloffRange = XE_GTAO_DEFAULT_FALLOFF_RANGE,
                SampleDistributionPower = XE_GTAO_DEFAULT_SAMPLE_DISTRIBUTION_POWER,
                ThinOccluderCompensation = XE_GTAO_DEFAULT_THIN_OCCLUDER_COMPENSATION,
                FinalValuePower = XE_GTAO_DEFAULT_FINAL_VALUE_POWER,
                DepthMIPSamplingOffset = XE_GTAO_DEFAULT_DEPTH_MIP_SAMPLING_OFFSET,
            };

            // If using TAA then set noiseIndex to frameIndex % 64 - otherwise use 0
            public static unsafe void GTAOUpdateConstants(ref GTAOConstantsCS consts, int viewportWidth, int viewportHeight, in GTAOSettings settings,
                float4x4 projMatrix,
                bool rowMajor, uint frameCounter)
            {
                float* pProjMatrix = (float*) UnsafeUtility.AddressOf(ref projMatrix);

                consts.ViewportSize = int2(
                        viewportWidth, viewportHeight
                    )
                    ;
                consts.ViewportPixelSize = float2(
                        1.0f / viewportWidth, 1.0f / viewportHeight
                    )
                    ;

                float depthLinearizeMul =
                    rowMajor ? -pProjMatrix[3 * 4 + 2] : -pProjMatrix[3 + 2 * 4]; // float depthLinearizeMul = ( clipFar * clipNear ) / ( clipFar - clipNear );
                depthLinearizeMul *= -1;
                float depthLinearizeAdd =
                    rowMajor ? pProjMatrix[2 * 4 + 2] : pProjMatrix[2 + 2 * 4]; // float depthLinearizeAdd = clipFar / ( clipFar - clipNear );

                // correct the handedness issue. need to make sure this below is correct, but I think it is.
                if (depthLinearizeMul * depthLinearizeAdd < 0)
                    depthLinearizeAdd = -depthLinearizeAdd;
                
                consts.DepthUnpackConsts = float2(
                        depthLinearizeMul, depthLinearizeAdd
                    )
                    ;

                float tanHalfFOVY = 1.0f / (rowMajor ? pProjMatrix[1 * 4 + 1] : pProjMatrix[1 + 1 * 4]); // = tanf( drawContext.Camera.GetYFOV( ) * 0.5f );
                float tanHalfFOVX = 1.0F / (rowMajor ? pProjMatrix[0 * 4 + 0] : pProjMatrix[0 + 0 * 4]); // = tanHalfFOVY * drawContext.Camera.GetAspect( );
                consts.CameraTanHalfFOV = float2(
                        tanHalfFOVX, tanHalfFOVY
                    )
                    ;

                consts.NDCToViewMul = float2(
                        consts.CameraTanHalfFOV.x * 2.0f, consts.CameraTanHalfFOV.y * -2.0f
                    )
                    ;
                consts.NDCToViewAdd = float2(
                        consts.CameraTanHalfFOV.x * -1.0f, consts.CameraTanHalfFOV.y * 1.0f
                    )
                    ;

                consts.NDCToViewMul_x_PixelSize = float2(
                        consts.NDCToViewMul.x * consts.ViewportPixelSize.x, consts.NDCToViewMul.y * consts.ViewportPixelSize.y
                    )
                    ;

                consts.EffectRadius = settings.Radius;

                consts.EffectFalloffRange = settings.FalloffRange;
                consts.DenoiseBlurBeta =
                    settings.DenoisePasses == 0 ? 1e4f : 1.2f; // high value disables denoise - more elegant & correct way would be do set all edges to 0

                consts.RadiusMultiplier = settings.RadiusMultiplier;
                consts.SampleDistributionPower = settings.SampleDistributionPower;
                consts.ThinOccluderCompensation = settings.ThinOccluderCompensation;
                consts.FinalValuePower = settings.FinalValuePower;
                consts.DepthMIPSamplingOffset = settings.DepthMIPSamplingOffset;
                consts.NoiseIndex = (int) (settings.DenoisePasses > 0 ? frameCounter % 64 : 0);
                consts.Padding0 = 0;
            }
        }
    }
}