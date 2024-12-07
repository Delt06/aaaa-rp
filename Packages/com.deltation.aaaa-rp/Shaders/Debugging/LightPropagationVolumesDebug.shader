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

            uint  _DebugInstanceCountDimension;
            float _DebugSize;
            float _DebugIntensity;
            float _DebugClipDistance;

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
                const float  cellSize = (_LPVGridBoundsMin.x - _LPVGridBoundsMax.x) / GetLPVGridSize();
                const float3 cameraPositionWS = GetCameraPositionWS();

                const float3 pivotPositionWS = cameraPositionWS + cameraForwardWS * cellSize * _DebugInstanceCountDimension / 2;
                const int3   centerCellID = ComputeLPVCellID(pivotPositionWS);

                int3 localInstanceID;
                localInstanceID.x = instanceID % _DebugInstanceCountDimension;
                localInstanceID.y = instanceID / _DebugInstanceCountDimension % _DebugInstanceCountDimension;
                localInstanceID.z = instanceID / _DebugInstanceCountDimension / _DebugInstanceCountDimension;
                localInstanceID -= _DebugInstanceCountDimension / 2;

                const int3 globalCellID = centerCellID + localInstanceID;
                if (any(globalCellID < 0) || any(globalCellID) >= GetLPVGridSize())
                {
                    return (Varyings)0;
                }

                const float3 cellPositionWS = ComputeLPVCellCenter(globalCellID);

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
                const float3       normalWS = SafeNormalize(IN.normalWS);
                const LPVCellValue cellValue = SampleLPVGrid_PointFilter(IN.positionWS);
                return float4(_DebugIntensity * LPVMath::EvaluateRadiance(cellValue, normalWS), 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}