Shader "Hidden/AAAA/VisibilityBufferResolve"
{
    HLSLINCLUDE
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

            #include_with_pragmas "Packages/com.deltation.aaaa-rp/ShaderLibrary/Bindless.hlsl"
            #pragma editor_sync_compilation

            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/GBuffer.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Barycentric.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Instances.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Meshlets.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Materials.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Utils.hlsl"
            
            Varyings OverrideVert(Attributes input)
            {
                Varyings output = Vert(input);

                output.positionCS.z = UNITY_RAW_FAR_CLIP_VALUE * output.positionCS.w;

                return output;
            }

            GBufferOutput Frag(const Varyings IN)
            {
                const uint2 visibilityBufferPacked = SampleVisibilityBuffer(IN.texcoord); 
                const VisibilityBufferValue visibilityBufferValue = UnpackVisibilityBufferValue(visibilityBufferPacked); 

                const AAAAInstanceData instanceData = PullInstanceData(visibilityBufferValue.instanceID);
                const AAAAMeshlet meshlet = PullMeshletData(visibilityBufferValue.meshletID);
                const AAAAMaterialData materialData = PullMaterialData(instanceData.MaterialIndex);

                const uint3 indices = uint3(
                    PullIndex(meshlet, visibilityBufferValue.indexID + 0),
                    PullIndex(meshlet, visibilityBufferValue.indexID + 1),
                    PullIndex(meshlet, visibilityBufferValue.indexID + 2)
                );
                const AAAAMeshletVertex vertices[3] =
                {
                    PullVertex(meshlet, indices[0]),
                    PullVertex(meshlet, indices[1]),
                    PullVertex(meshlet, indices[2]),
                };

                const float3 positionWS[3] =
                {
                    TransformObjectToWorld(vertices[0].Position.xyz, instanceData.ObjectToWorldMatrix),
                    TransformObjectToWorld(vertices[1].Position.xyz, instanceData.ObjectToWorldMatrix),
                    TransformObjectToWorld(vertices[2].Position.xyz, instanceData.ObjectToWorldMatrix),
                };

                const float4 positionCS[3] =
                {
                    TransformWorldToHClip(positionWS[0]),
                    TransformWorldToHClip(positionWS[1]),
                    TransformWorldToHClip(positionWS[2]),
                };

                const float2                 pixelNDC = ScreenCoordsToNDC(IN.positionCS);
                const BarycentricDerivatives barycentric = CalculateFullBarycentric(positionCS[0], positionCS[1], positionCS[2], pixelNDC, _ScreenSize.zw);

                const InterpolatedUV uv = InterpolateUV(barycentric, vertices[0], vertices[1], vertices[2]);
                const float3 albedo = SampleAlbedo(uv, materialData).rgb;

                const float3 normalOS =
                    SafeNormalize(
                    InterpolateWithBarycentricNoDerivatives(barycentric, vertices[0].Normal.xyz, vertices[1].Normal.xyz, vertices[2].Normal.xyz)
                );
                float3 normalWS = TransformObjectToWorldNormal(normalOS, instanceData.WorldToObjectMatrix);

                UNITY_BRANCH
                if (materialData.NormalsIndex != (uint)NO_TEXTURE_INDEX)
                {
                    const float4 tangentOS = InterpolateWithBarycentricNoDerivatives(barycentric,
                                                                                     vertices[0].Tangent, vertices[1].Tangent, vertices[2].Tangent);
                    const float4 tangentWS = float4(TransformObjectToWorldDir(tangentOS.xyz, instanceData.ObjectToWorldMatrix), tangentOS.w);
                    const float3 bitangentWS = tangentWS.w * cross(normalWS, tangentWS.xyz);

                    const float3x3 tangentToWorld = float3x3(tangentWS.xyz, bitangentWS, normalWS);
                    const float3   normalTS = SampleNormalTS(uv, materialData);
                    normalWS = TransformTangentToWorld(normalTS, tangentToWorld, true);
                }

                const MaterialMasks materialMasks = SampleMasks(uv, materialData);

                GBufferValue gbufferValue;
                gbufferValue.albedo = albedo;
                gbufferValue.normalWS = normalWS;
                gbufferValue.roughness = materialMasks.roughness;
                gbufferValue.metallic = materialMasks.metallic;

                return PackGBufferOutput(gbufferValue);

            }
            ENDHLSL
        }
    }

    Fallback Off
}
