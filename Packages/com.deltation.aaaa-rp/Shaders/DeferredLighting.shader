Shader "Hidden/AAAA/DeferredLighting"
{
    Properties
    {
    }
    
    HLSLINCLUDE

        #pragma target 2.0
        #pragma editor_sync_compilation
        #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Debug/DebuggingFullscreen.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "AAAAPipeline" }

        Pass
        {
            ZWrite Off
            ZTest Greater
            ZClip Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex OverrideVert
            #pragma fragment Frag

            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/GBuffer.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/PBR.hlsl"
            
            Varyings OverrideVert(Attributes input)
            {
                Varyings output = Vert(input);

                output.positionCS.z = UNITY_RAW_FAR_CLIP_VALUE * output.positionCS.w;

                return output;
            }

            float4 Frag(const Varyings IN) : SV_Target
            {
                const GBufferOutput gbuffer = SampleGBuffer(IN.texcoord);

                SurfaceData surfaceData;
                surfaceData.albedo = gbuffer.albedo;
                surfaceData.normalWS = gbuffer.normalsWS;

                const float3 lighting = ComputeLightingPBR(surfaceData);
                return float4(lighting, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
