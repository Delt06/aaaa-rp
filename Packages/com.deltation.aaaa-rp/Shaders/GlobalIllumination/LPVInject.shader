Shader "Hidden/AAAA/LPV/Inject"
{
    Properties {}
    SubShader
    {
        Pass
        {
            Name "LPV Inject (Point Rendering)"
            Blend One One
            ZWrite Off
            ZTest Off
            Cull Off
            ZClip Off

            HLSLPROGRAM
            // Sources:
            // - Kaplanyan, Anton. (2009). Light Propagation Volumes in CryEngine 3.
            // - https://github.com/mafian89/Light-Propagation-Volumes/blob/master/shaders/lightInject.frag
            // - https://ericpolman.com/2016/06/28/light-propagation-volumes/

            #pragma vertex VS
            #pragma fragment PS

            #pragma multi_compile_local _ BLOCKING_POTENTIAL

            #include_with_pragmas "Packages/com.deltation.aaaa-rp/ShaderLibrary/Bindless.hlsl"

            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Depth.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                #ifdef BLOCKING_POTENTIAL
                float4 blockingPotentialSH : BLOCKING_POTENTIAL_SH;
                #else
                float4 redSH : RED_SH;
                float4 greenSH : GREEN_SH;
                float4 blueSH : BLUE_SH;
                #endif
            };

            struct FragmentOut
            {
                #ifdef BLOCKING_POTENTIAL
                float4 blockingPotentialSH : SV_Target0;
                #else
                float4 redSH : SV_Target0;
                float4 greenSH : SV_Target1;
                float4 blueSH : SV_Target2;
                #endif
            };

            float4 _RSMResolution;
            float4 _LightDirectionWS;
            float4 _LightColor;
            float2 _Biases;

            TYPED_TEXTURE2D(float4, _RSMPositionMap);
            TYPED_TEXTURE2D(float2, _RSMNormalMap);
            TYPED_TEXTURE2D(float3, _RSMFluxMap);

            #define LIGHT_DEPTH_BIAS (_Biases.x)
            #define NORMAL_DEPTH_BIAS (_Biases.y)
            #define SURFEL_WEIGHT (5 * _RSMResolution.z)

            Varyings DiscardVertex()
            {
                Varyings varyings = (Varyings)0;
                varyings.positionCS = FLT_NAN;
                return varyings;
            }

            RsmOutput FetchRSM(const uint2 texelID)
            {
                RsmOutput output;
                output.positionWS = LOAD_TEXTURE2D_LOD(_RSMPositionMap, texelID, 0).xyz;
                output.packedNormalWS = LOAD_TEXTURE2D_LOD(_RSMNormalMap, texelID, 0).xy;
                output.flux = LOAD_TEXTURE2D_LOD(_RSMFluxMap, texelID, 0).xyz;
                return output;
            }

            float3 ComputeBiasedGridPositionWS(const RsmValue rsmValue)
            {
                float3 gridPositionWS = rsmValue.positionWS;
                gridPositionWS += rsmValue.normalWS * NORMAL_DEPTH_BIAS;
                gridPositionWS += _LightDirectionWS.xyz * LIGHT_DEPTH_BIAS;
                return gridPositionWS;
            }

            Varyings VS(const uint vertexID : SV_VertexID)
            {
                Varyings OUT;

                const uint2 resolution = (uint2)_RSMResolution.xy;
                const uint2 rsmTexelID = uint2(vertexID / resolution.x, vertexID % resolution.x);

                const RsmValue rsmValue = RsmValue::Unpack(FetchRSM(rsmTexelID));

                const float3 gridPositionWS = ComputeBiasedGridPositionWS(rsmValue);
                #ifdef BLOCKING_POTENTIAL
                const int3 resultCellID = LPV::ComputeBlockingPotentialCellID(gridPositionWS);
                #else
                const int3 resultCellID = LPV::ComputeCellID(gridPositionWS);
                #endif

                #ifndef BLOCKING_POTENTIAL
                UNITY_BRANCH
                if (dot(rsmValue.flux, rsmValue.flux) == 0.0)
                {
                    return DiscardVertex();
                }
                #endif

                UNITY_BRANCH
                if (!all(0 <= resultCellID && resultCellID < LPV::GetGridSize()))
                {
                    return DiscardVertex();
                }

                const float         NdotL = saturate(dot(_LightDirectionWS, rsmValue.normalWS));
                const LPV_CHANNEL_T normalSH = LPVMath::DirToCosineLobe(-rsmValue.normalWS);
                const LPV_CHANNEL_T shCoefficients = NdotL * SURFEL_WEIGHT * INV_PI * normalSH;

                #ifdef BLOCKING_POTENTIAL
                const float invCellSize = LPV::GetGridSize() / (_LPVGridBoundsMax.x - _LPVGridBoundsMin.x);
                const float blockingPotential = saturate(NdotL * SURFEL_WEIGHT * invCellSize * invCellSize);
                if (blockingPotential == 0)
                {
                    return DiscardVertex();
                }
                #endif

                #ifdef BLOCKING_POTENTIAL
                OUT.blockingPotentialSH = normalSH * blockingPotential;
                #else
                const float3 flux = rsmValue.flux * _LightColor.rgb;
                OUT.redSH = shCoefficients * flux.r;
                OUT.greenSH = shCoefficients * flux.g;
                OUT.blueSH = shCoefficients * flux.b;
                #endif

                const float2 pixelCoord = 0.5 + LPV::CellIDToPackedID(resultCellID);
                const float  invGridSize = rcp(LPV::GetGridSize());
                float2       positionNDC = (pixelCoord * float2(invGridSize * invGridSize, invGridSize)) * 2 - 1;
                #ifdef UNITY_UV_STARTS_AT_TOP
                positionNDC.y *= -1;
                #endif
                OUT.positionCS = float4(positionNDC, 0, 1);
                return OUT;
            }

            FragmentOut PS(const Varyings IN)
            {
                FragmentOut OUT;
                #ifdef BLOCKING_POTENTIAL
                OUT.blockingPotentialSH = IN.blockingPotentialSH;
                #else
                OUT.redSH = IN.redSH;
                OUT.greenSH = IN.greenSH;
                OUT.blueSH = IN.blueSH;
                #endif
                return OUT;
            }
            ENDHLSL
        }

    }
}