Shader "Hidden/AAAA/LightPropagationVolumesDebug"
{
    HLSLINCLUDE
    #pragma editor_sync_compilation

    #include_with_pragmas "Packages/com.deltation.aaaa-rp/ShaderLibrary/Bindless.hlsl"
    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "AAAAPipeline"
            "LightMode" = "Forward"
            "RenderQueue" = "Transparent"
        }

        Pass
        {
            Cull OFF

            HLSLPROGRAM
            #pragma vertex VS
            #pragma fragment PS

            #include_with_pragmas "Packages/com.deltation.aaaa-rp/Shaders/GlobalIllumination/LPVPragma.hlsl"

            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/CameraDepth.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/GBuffer.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.deltation.aaaa-rp/Runtime/Debugging/AAAADebugDisplaySettingsRendering.cs.hlsl"

            uint  _DebugInstanceCountDimension;
            uint  _DebugMode;
            float _DebugSize;
            float _DebugIntensity;
            float _DebugClipDistance;

            TYPED_TEXTURE3D(LPV_CHANNEL_T, _BlockingPotentialSH);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : POSITION_WS;
                float3 normalWS : NORMAL_WS;
                float  clipDistance : SV_ClipDistance;
            };

            Varyings VS(const Attributes IN, const uint instanceID : INSTANCEID_SEMANTIC)
            {
                const float3 cameraForwardWS = normalize(-GetViewForwardDir(UNITY_MATRIX_V));
                const float  cellSize = (_LPVGridBoundsMin.x - _LPVGridBoundsMax.x) / LPV::GetGridSize();
                const float3 cameraPositionWS = GetCameraPositionWS();

                const float3 pivotPositionWS = cameraPositionWS + cameraForwardWS * cellSize * _DebugInstanceCountDimension / 2;
                const int3   centerCellID = LPV::ComputeCellID(pivotPositionWS);

                int3 localInstanceID;
                localInstanceID.x = instanceID % _DebugInstanceCountDimension;
                localInstanceID.y = instanceID / _DebugInstanceCountDimension % _DebugInstanceCountDimension;
                localInstanceID.z = instanceID / _DebugInstanceCountDimension / _DebugInstanceCountDimension;
                localInstanceID -= _DebugInstanceCountDimension / 2;

                const int3 globalCellID = centerCellID + localInstanceID;
                if (any(globalCellID < 0) || any(globalCellID) >= LPV::GetGridSize())
                {
                    return (Varyings)0;
                }

                const float3 cellPositionWS = LPV::ComputeCellCenter(globalCellID);

                Varyings OUT;

                const float3 positionWS = cellPositionWS + _DebugSize * IN.positionOS;
                OUT.positionWS = positionWS;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.normalWS = IN.normalOS;
                OUT.clipDistance = Length2(positionWS - cameraPositionWS) - _DebugClipDistance * _DebugClipDistance;

                return OUT;
            }

            float4 PS(const Varyings IN) : SV_Target
            {
                float3       result;
                const float3 normalWS = SafeNormalize(IN.normalWS);

                switch (_DebugMode)
                {
                case AAAALIGHTPROPAGATIONVOLUMESDEBUGMODE_RADIANCE:
                    {
                        const LPVCellValue cellValue = LPV::SampleGrid(IN.positionWS, sampler_PointClamp);
                        result = LPVMath::EvaluateRadiance(cellValue, normalWS);
                        break;
                    }
                case AAAALIGHTPROPAGATIONVOLUMESDEBUGMODE_BLOCKING_POTENTIAL:
                    {
                        const float3 gridUV = LPV::ComputeGridUV(IN.positionWS);
                        const float4 blockingPotentialSH = SAMPLE_TEXTURE3D_LOD(_BlockingPotentialSH, sampler_PointClamp, gridUV, 0);
                        result = saturate(LPVMath::EvaluateBlockingPotential(blockingPotentialSH, normalWS));
                        break;
                    }
                default:
                    {
                        result = 0;
                        break;
                    }
                }

                return float4(_DebugIntensity * result, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}