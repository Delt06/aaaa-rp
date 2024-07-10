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
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Barycentric.hlsl"

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

            GBufferOutput Frag(const Varyings IN)
            {
                const uint2 visibilityValue = asuint(FragBlit(IN, sampler_PointClamp).xy);
                uint instanceID, meshletID, indexID; 
                UnpackVisibilityBufferValue(visibilityValue, instanceID, meshletID, indexID);

                const AAAAPerInstanceData perInstanceData = _PerInstanceData[instanceID];
                const AAAAMeshlet meshlet = _Meshlets[meshletID];

                const uint3 indices = uint3(
                    PullIndex(meshlet, indexID + 0),
                    PullIndex(meshlet, indexID + 1),
                    PullIndex(meshlet, indexID + 2)
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
                const float3 albedo = float3(uv.uv, 0);

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
