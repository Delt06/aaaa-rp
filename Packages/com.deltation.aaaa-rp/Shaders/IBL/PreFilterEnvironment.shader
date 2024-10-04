Shader "Hidden/AAAA/IBL/PreFilterEnvironment"
{
    HLSLINCLUDE
    #pragma editor_sync_compilation

    #include_with_pragmas "Packages/com.deltation.aaaa-rp/ShaderLibrary/Bindless.hlsl"
    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/BRDF.hlsl"
    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/IBL/Utils.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
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
            Name "Pre-Filter Environment"

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
            float2 _SourceResolution;

            float  _Roughness;
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
                // https://learnopengl.com/PBR/IBL/Specular-IBL
                float3 N = normalize(IN.normal);
                float3 R = N;
                float3 V = R;

                const uint SAMPLE_COUNT = 1024u;
                float      totalWeight = 0.0;
                float3     prefilteredColor = 0;
                for (uint i = 0u; i < SAMPLE_COUNT; ++i)
                {
                    float2 Xi = Hammersley(i, SAMPLE_COUNT);
                    float3 H = ImportanceSampleGGX(Xi, N, _Roughness);
                    float3 L = normalize(2.0 * dot(V, H) * H - V);

                    float NdotL = max(dot(N, L), 0.0);
                    if (NdotL > 0.0)
                    {
                        // sample from the environment's mip level based on roughness/pdf
                        float D = DistributionGGX(N, H, _Roughness);
                        float NdotH = max(dot(N, H), 0.0);
                        float HdotV = max(dot(H, V), 0.0);
                        float pdf = D * NdotH / (4.0 * HdotV) + 0.0001;

                        float saTexel = 4.0 * PI / (6.0 * _SourceResolution.x * _SourceResolution.y);
                        float saSample = 1.0 / (float(SAMPLE_COUNT) * pdf + 0.0001);

                        float mipLevel = _Roughness == 0.0 ? 0.0 : 0.5 * log2(saSample / saTexel);

                        prefilteredColor += SAMPLE_TEXTURECUBE_LOD(_Source, sampler_Source, L, mipLevel).rgb * NdotL;
                        totalWeight += NdotL;
                    }
                }

                prefilteredColor = prefilteredColor / totalWeight;

                return float4(prefilteredColor, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}