Shader "Hidden/AAAA/VisibilityBufferResolve"
{
    HLSLINCLUDE
    #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
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

            float ComputeNormalFlipSign(const AAAAMaterialData materialData, const float3 positionWS[3])
            {
                float normalSign = 1;

                UNITY_BRANCH
                if (materialData.RendererListID & AAAARENDERERLISTID_CULL_OFF)
                {
                    const float3 autoNormalWS = cross(normalize(positionWS[0] - positionWS[1]), normalize(positionWS[2] - positionWS[0]));
                    const float3 viewForwardDirWS = GetViewForwardDir(UNITY_MATRIX_V);
                    if (dot(autoNormalWS, viewForwardDirWS) < 0)
                    {
                        normalSign = -1;
                    }
                }

                return normalSign;
            }

            Varyings OverrideVert(Attributes input)
            {
                Varyings output = Vert(input);

                output.positionCS.z = UNITY_RAW_FAR_CLIP_VALUE * output.positionCS.w;

                return output;
            }

            GBufferOutput Frag(const Varyings IN)
            {
                const uint2                 visibilityBufferPacked = LoadVisibilityBuffer(IN.positionCS.xy);
                const VisibilityBufferValue visibilityBufferValue = UnpackVisibilityBufferValue(visibilityBufferPacked);

                const AAAAInstanceData instanceData = PullInstanceData(visibilityBufferValue.instanceID);
                const AAAAMeshlet      meshlet = PullMeshletData(visibilityBufferValue.meshletID);
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
                const BarycentricDerivatives barycentric = CalculateFullBarycentric(
                    positionCS[0], positionCS[1], positionCS[2], pixelNDC, _ScreenSize.zw);

                InterpolatedUV interpolatedUV = InterpolateUV(barycentric, vertices[0], vertices[1], vertices[2]);
                interpolatedUV.AddTilingOffset(materialData.TextureTilingOffset);
                const float4 textureAlbedo = SampleAlbedoTextureGrad(interpolatedUV, materialData);
                const float3 albedo = textureAlbedo.rgb * materialData.AlbedoColor.rgb;

                const float  normalFlipSing = ComputeNormalFlipSign(materialData, positionWS);
                const float3 vertexNormalWS[3] =
                {
                    normalFlipSing * TransformObjectToWorldNormal(SafeNormalize(vertices[0].Normal.xyz), instanceData.WorldToObjectMatrix),
                    normalFlipSing * TransformObjectToWorldNormal(SafeNormalize(vertices[1].Normal.xyz), instanceData.WorldToObjectMatrix),
                    normalFlipSing * TransformObjectToWorldNormal(SafeNormalize(vertices[2].Normal.xyz), instanceData.WorldToObjectMatrix),
                };
                const BarycentricDerivatives barycentricVertexNormalWS = InterpolateWithBarycentric(
                    barycentric, vertexNormalWS[0], vertexNormalWS[1], vertexNormalWS[2]);
                float3 normalWS = SafeNormalize(barycentricVertexNormalWS.lambda);

                UNITY_BRANCH
                if (materialData.NormalsIndex != (uint)NO_TEXTURE_INDEX)
                {
                    const float4 tangentOS = InterpolateWithBarycentricNoDerivatives(barycentric,
                                               vertices[0].Tangent, vertices[1].Tangent, vertices[2].Tangent);
                    const float4 tangentWS = float4(TransformObjectToWorldDir(tangentOS.xyz, instanceData.ObjectToWorldMatrix), tangentOS.w);
                    const float3 bitangentWS = tangentWS.w * cross(normalWS, tangentWS.xyz);

                    const float3x3 tangentToWorld = float3x3(tangentWS.xyz, bitangentWS, normalWS);
                    const float3   normalTS = SampleNormalTSGrad(interpolatedUV, materialData);
                    normalWS = TransformTangentToWorld(normalTS, tangentToWorld, true);
                }

                const MaterialMasks materialMasks = SampleMasksGrad(interpolatedUV, materialData);

                GBufferValue gbufferValue;
                gbufferValue.albedo = albedo;
                gbufferValue.emission = textureAlbedo.rgb * materialData.Emission.rgb;
                gbufferValue.normalWS = normalWS;
                gbufferValue.roughness = materialMasks.roughness;
                gbufferValue.metallic = materialMasks.metallic;
                gbufferValue.materialFlags = materialData.MaterialFlags;

                UNITY_BRANCH
                if (materialData.GeometryFlags & AAAAGEOMETRYFLAGS_SPECULAR_AA)
                {
                    const float screenSpaceVariance = materialData.SpecularAAScreenSpaceVariance;
                    const float threshold = materialData.SpecularAAThreshold;
                    gbufferValue.roughness = GeometricNormalFiltering(gbufferValue.roughness, barycentricVertexNormalWS,
                             screenSpaceVariance, threshold);
                }

                return PackGBufferOutput(gbufferValue);

            }
            ENDHLSL
        }
    }

    Fallback Off
}