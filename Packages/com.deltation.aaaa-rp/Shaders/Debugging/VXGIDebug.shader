Shader "Hidden/AAAA/VXGIDebug"
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

            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/CameraDepth.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VXGI.hlsl"
            #include "Packages/com.deltation.aaaa-rp/Runtime/Debugging/AAAADebugDisplaySettingsRendering.cs.hlsl"

            TYPED_TEXTURE3D(float4, _GridAlbedo);
            TYPED_TEXTURE3D(float3, _GridEmission);
            TYPED_TEXTURE3D(float4, _GridRadiance);
            TYPED_TEXTURE3D(float2, _GridNormals);

            uint _DebugMode;
            uint _GridMipLevel;

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 color : COLOR;
            };

            Varyings VS(const Attributes IN, const uint instanceID : INSTANCEID_SEMANTIC)
            {
                VXGI::Grid grid = VXGI::Grid::LoadLevel(_GridMipLevel);

                const float3 voxelID = grid.FlatToVoxelID(instanceID);
                const float3 positionWS = grid.voxelSizeWS * IN.positionOS + grid.TransformGridToWorldSpace(voxelID + 0.5);
                const float4 albedo = _GridAlbedo.mips[_GridMipLevel][voxelID];

                if (albedo.a == 0)
                {
                    return (Varyings)0;
                }

                Varyings OUT;
                OUT.positionCS = TransformWorldToHClip(positionWS);

                float3 outputColor;

                switch (_DebugMode)
                {
                case AAAAVXGIDEBUGMODE_ALBEDO:
                    outputColor = albedo.rgb;
                    break;
                case AAAAVXGIDEBUGMODE_EMISSION:
                    outputColor = _GridEmission.mips[_GridMipLevel][voxelID];
                    break;
                case AAAAVXGIDEBUGMODE_RADIANCE:
                    outputColor = VXGI::Packing::UnpackRadiance(_GridRadiance.mips[_GridMipLevel][voxelID]).rgb;
                    break;
                case AAAAVXGIDEBUGMODE_NORMALS:
                    outputColor = VXGI::Packing::UnpackNormal(_GridNormals.mips[_GridMipLevel][voxelID]) * 0.5 + 0.5;
                    break;
                default:
                    outputColor = 0;
                }

                OUT.color = outputColor;

                return OUT;
            }

            float4 PS(const Varyings IN) : SV_Target
            {
                return float4(IN.color, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}