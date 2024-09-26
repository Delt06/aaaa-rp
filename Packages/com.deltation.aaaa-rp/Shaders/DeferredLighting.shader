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
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/CameraDepth.hlsl"
            
            Varyings OverrideVert(Attributes input)
            {
                Varyings output = Vert(input);

                output.positionCS.z = UNITY_RAW_FAR_CLIP_VALUE * output.positionCS.w;

                return output;
            }

            float4 Frag(const Varyings IN) : SV_Target
            {
                const GBufferValue gbuffer = SampleGBuffer(IN.texcoord);
                const float deviceDepth = SampleDeviceDepth(IN.texcoord);

                SurfaceData surfaceData;
                surfaceData.albedo = gbuffer.albedo;
                surfaceData.roughness = gbuffer.roughness;
                surfaceData.metallic = gbuffer.metallic;
                surfaceData.normalWS = gbuffer.normalWS;
                surfaceData.positionWS = ComputeWorldSpacePosition(IN.texcoord, deviceDepth, UNITY_MATRIX_I_VP);

                const float3 lighting = ComputeLightingPBR(surfaceData);
                return float4(lighting, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
