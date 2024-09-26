Shader "Hidden/AAAA/GBufferDebug"
{
    HLSLINCLUDE
        #pragma target 2.0
        #pragma editor_sync_compilation
        
        #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "AAAAPipeline" }

        Pass
        {
            ZWrite Off
            ZTest Off
            ZClip Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/CameraDepth.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/GBuffer.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Math.hlsl"
            #include "Packages/com.deltation.aaaa-rp/Runtime/Debugging/AAAADebugDisplaySettingsRendering.cs.hlsl"

            uint _GBufferDebugMode;
            float2 _GBufferDebugDepthRemap;

            float4 Frag(const Varyings IN) : SV_Target
            {
                const GBufferValue value = SampleGBuffer(IN.texcoord);
                const float depth = SampleLinearDepth(IN.texcoord);

                float3 resultColor;

                switch (_GBufferDebugMode)
                {
                case AAAAGBUFFERDEBUGMODE_DEPTH:
                    {
                        const float remappedDepth = InverseLerpUnclamped(_GBufferDebugDepthRemap.x, _GBufferDebugDepthRemap.y, depth);
                        resultColor = lerp(float3(0, 1, 0), float3(1, 0, 0), saturate(remappedDepth));
                        break;
                    }
                case AAAAGBUFFERDEBUGMODE_ALBEDO:
                    {
                        resultColor = value.albedo;
                        break;
                    }
                case AAAAGBUFFERDEBUGMODE_NORMALS:
                    {
                        resultColor = value.normalWS * 0.5 + 0.5;
                        break;
                    }
                default:
                    {
                        resultColor = 0;
                        break;
                    }
                }

                return float4(resultColor, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
