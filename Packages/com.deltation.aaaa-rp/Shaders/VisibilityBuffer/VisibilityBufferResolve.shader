Shader "Hidden/AAAA/VisibilityBufferResolve"
{
    HLSLINCLUDE
        #pragma target 2.0
        
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
                const VisibilityBufferValue value = SampleVisibilityBuffer(IN.texcoord); 

                const AAAAInstanceData instanceData = PullInstanceData(value.instanceID);
                const uint meshletID = instanceData.MeshletStartOffset + value.relativeMeshletID;
                const AAAAMeshlet meshlet = PullMeshletData(meshletID);
                const AAAAMaterialData materialData = PullMaterialData(instanceData.MaterialIndex);

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
                const float3 normalWS = TransformObjectToWorldNormal(normalOS, instanceData.WorldToObjectMatrix);

                return ConstructGBufferOutput(albedo, normalWS);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
