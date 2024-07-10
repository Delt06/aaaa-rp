Shader "Hidden/AAAA/VisibilityBufferResolve"
{
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

                #pragma enable_d3d11_debug_symbols

                #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/GBuffer.hlsl"
                #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Utils.hlsl"

                Varyings OverrideVert(Attributes input)
                {
                    Varyings output = Vert(input);

                    output.positionCS.z = UNITY_RAW_FAR_CLIP_VALUE * output.positionCS.w;

                    return output;
                }

                static AAAAPerInstanceData _PerInstanceDataCached;

                float3 TransformObjectToWorldNormal(const float3 normalOS, const float4x4 worldToObject, const bool doNormalize = true)
                {
                    float3 normalWS = mul(normalOS, (float3x3)worldToObject);
                    if (doNormalize)
                    {
                        return SafeNormalize(normalWS);
                    }
                    return normalWS;
                }

                GBufferOutput Frag(const Varyings IN)
                {
                    const uint2 visibilityValue = asuint(FragBlit(IN, sampler_PointClamp).xy);
                    uint instanceID, meshletID, indexID; 
                    UnpackVisibilityBufferValue(visibilityValue, instanceID, meshletID, indexID);

                    const AAAAPerInstanceData perInstanceData = _PerInstanceData[instanceID];
                    const AAAAMeshlet meshlet = _Meshlets[meshletID];

                    const uint indices[3] =
                    {
                        PullIndex(meshlet, indexID + 0),
                        PullIndex(meshlet, indexID + 1),
                        PullIndex(meshlet, indexID + 2)
                    };
                    const AAAAMeshletVertex vertices[3] =
                    {
                        PullVertex(meshlet, indices[0]),
                        PullVertex(meshlet, indices[1]),
                        PullVertex(meshlet, indices[2])
                    };

                    const float3 normalWS = TransformObjectToWorldNormal(vertices[0].Normal, perInstanceData.WorldToObjectMatrix);
                    return ConstructGBufferOutput(1, normalWS);
                }
            ENDHLSL
        }
    }

    Fallback Off
}
