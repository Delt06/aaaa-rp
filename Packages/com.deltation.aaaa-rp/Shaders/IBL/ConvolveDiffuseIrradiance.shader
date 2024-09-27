Shader "Hidden/AAAA/ConvolveDiffuseIrradiance"
{
    HLSLINCLUDE
    #pragma target 2.0
    #pragma editor_sync_compilation

    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "AAAAPipeline"
        }

        Pass
        {
            ZWrite Off
            ZTest Off
            ZClip Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normal : NORMAL;
            };

            TEXTURECUBE(_Source);
            SAMPLER(sampler_Source);
            float4 _SourceHDRDecodeValues;

            float4 _Forward;
            float4 _Up;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float4 pos = GetFullScreenTriangleVertexPosition(input.vertexID);

                output.positionCS = pos;

                float2 uv = GetFullScreenTriangleTexCoord(input.vertexID);
                uv -= 0.5f; // [0, 1] -> [-0.5, 0.5]
                uv *= -1.0f; // flip the vertical coord

                float3 forward = _Forward.xyz;
                float3 up = _Up.xyz;
                float3 right = cross(forward, up);
                output.normal = forward * 0.5f + up * uv.y + right * uv.x;

                return output;
            }

            float4 Frag(const Varyings IN) : SV_Target
            {
                // https://learnopengl.com/PBR/IBL/Diffuse-irradiance
                float3 irradiance = 0.0;

                float3 normal = normalize(IN.normal);
                float3 up = float3(0.0, 1.0, 0.0);
                float3 right = normalize(cross(up, normal));
                up = normalize(cross(normal, right));

                float sampleDelta = 0.025;
                float nrSamples = 0.0;

                for (float phi = 0.0; phi < 2.0 * PI; phi += sampleDelta)
                {
                    for (float theta = 0.0; theta < 0.5 * PI; theta += sampleDelta)
                    {
                        // spherical to cartesian (in tangent space)
                        float3 tangentSample = float3(sin(theta) * cos(phi), sin(theta) * sin(phi), cos(theta));
                        // tangent space to world
                        float3 sampleVec = tangentSample.x * right + tangentSample.y * up + tangentSample.z * normal;

                        const float3 environmentSample = DecodeHDREnvironment(
                            SAMPLE_TEXTURECUBE_LOD(_Source, sampler_Source, sampleVec, 0), _SourceHDRDecodeValues);
                        irradiance += environmentSample * cos(theta) * sin(theta);
                        nrSamples++;
                    }
                }

                irradiance = PI * irradiance * (1.0 / nrSamples);
                return float4(irradiance, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}