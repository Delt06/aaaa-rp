Shader "Hidden/AAAA/VisibilityBufferResolve"
{
    Properties
    {
        _Albedo ("Albedo", 2D) = "white" {}
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
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Barycentric.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Meshlets.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Utils.hlsl"

            TEXTURE2D(_Albedo);
            SAMPLER(sampler_Albedo);

            Varyings OverrideVert(Attributes input)
            {
                Varyings output = Vert(input);

                output.positionCS.z = UNITY_RAW_FAR_CLIP_VALUE * output.positionCS.w;

                return output;
            }

            float3 TransformObjectToWorld(const float3 positionOS, const float4x4 objectToWorldMatrix)
            {
                return mul(objectToWorldMatrix, float4(positionOS, 1.0f)).xyz;
            }

            float3 TransformObjectToWorldNormal(const float3 normalOS, const float4x4 worldToObject, const bool doNormalize = true)
            {
                float3 normalWS = mul(normalOS, (float3x3)worldToObject);
                if (doNormalize)
                {
                    return SafeNormalize(normalWS);
                }
                return normalWS;
            }

            float2 ScreenCoordsToNDC(float4 screenCoords)
            {
                float2 ndc = screenCoords.xy * _ScreenSize.zw * 2 - 1;
#ifdef UNITY_UV_STARTS_AT_TOP
                ndc.y *= -1;
#endif
                return ndc;
            }

            struct UV
            {
                float2 uv;
                float2 ddx;
                float2 ddy;
            };

            UV InterpolateUV(const BarycentricDerivatives barycentric, const AAAAMeshletVertex v0, const AAAAMeshletVertex v1, const AAAAMeshletVertex v2)
            {
                const float3 u = InterpolateWithBarycentric(barycentric, v0.UV.x, v1.UV.x, v2.UV.x);
                const float3 v = InterpolateWithBarycentric(barycentric, v0.UV.y, v1.UV.y, v2.UV.y);

                UV uv;
                uv.uv = float2(u.x, v.x);
                uv.ddx = float2(u.y, v.y);
                uv.ddy = float2(u.z, v.z);
                return uv;
            }

            float4 SampleAlbedo(const UV uv)
            {
                return SAMPLE_TEXTURE2D_GRAD(_Albedo, sampler_Albedo, uv.uv, uv.ddx, uv.ddy);
            }

            GBufferOutput Frag(const Varyings IN)
            {
                const VisibilityBufferValue value = SampleVisibilityBuffer(IN.texcoord); 

                const AAAAPerInstanceData perInstanceData = _PerInstanceData[value.instanceID];
                const AAAAMeshlet meshlet = _Meshlets[value.meshletID];

                const uint3 indices = uint3(
                    PullIndex(meshlet, value.indexID + 0),
                    PullIndex(meshlet, value.indexID + 1),
                    PullIndex(meshlet, value.indexID + 2)
                );
                const AAAAMeshletVertex vertices[3] =
                {
                    PullVertex(meshlet, indices[0]),
                    PullVertex(meshlet, indices[1]),
                    PullVertex(meshlet, indices[2]),
                };

                const float3 positionWS[3] =
                {
                    TransformObjectToWorld(vertices[0].Position.xyz, perInstanceData.ObjectToWorldMatrix),
                    TransformObjectToWorld(vertices[1].Position.xyz, perInstanceData.ObjectToWorldMatrix),
                    TransformObjectToWorld(vertices[2].Position.xyz, perInstanceData.ObjectToWorldMatrix),
                };

                const float4 positionCS[3] =
                {
                    TransformWorldToHClip(positionWS[0]),
                    TransformWorldToHClip(positionWS[1]),
                    TransformWorldToHClip(positionWS[2]),
                };

                const float2                 pixelNDC = ScreenCoordsToNDC(IN.positionCS);
                const BarycentricDerivatives barycentric = CalculateFullBarycentric(positionCS[0], positionCS[1], positionCS[2], pixelNDC, _ScreenSize.zw);

                const UV uv = InterpolateUV(barycentric, vertices[0], vertices[1], vertices[2]);
                const float3 albedo = SampleAlbedo(uv).rgb;

                const float3 normalOS =
                    SafeNormalize(
                    InterpolateWithBarycentricNoDerivatives(barycentric, vertices[0].Normal.xyz, vertices[1].Normal.xyz, vertices[2].Normal.xyz)
                );
                const float3 normalWS = TransformObjectToWorldNormal(normalOS, perInstanceData.WorldToObjectMatrix);

                return ConstructGBufferOutput(albedo, normalWS);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
