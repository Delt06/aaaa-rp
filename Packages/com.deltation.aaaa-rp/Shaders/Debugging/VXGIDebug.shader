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
                VXGI::Grid grid = VXGI::Grid::Load();

                const float3 voxelID = grid.FlatToVoxelID(instanceID);
                const float3 positionWS = grid.voxelSizeWS * IN.positionOS + grid.TransformGridToWorldSpace(voxelID + 0.5);
                const float4 albedo = _GridAlbedo[voxelID];

                if (albedo.a == 0)
                {
                    return (Varyings)0;
                }

                Varyings OUT;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.color = albedo.rgb;
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