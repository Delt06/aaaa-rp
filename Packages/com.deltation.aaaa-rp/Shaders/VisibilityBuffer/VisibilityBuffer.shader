Shader "Hidden/AAAA/VisibilityBuffer"
{
    Properties
    {
        
    }
    SubShader
    {
        Pass
        {
            Tags {"LightMode" = "Visibility"}
            
            HLSLPROGRAM

            #pragma vertex VS
            #pragma fragment PS

            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Instances.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Meshlets.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Utils.hlsl"

            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawArgs
            #include "UnityIndirect.cginc"

            ByteAddressBuffer _MeshletRenderRequests;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation uint2 visibilityValue : VISIBILITY_VALUE;
            };

            uint PullIndexChecked(const AAAAMeshlet meshlet, const uint indexID)
            {
                if (indexID >= meshlet.TriangleCount * 3)
                {
                    return -1;
                }
                return PullIndex(meshlet, indexID);
            }

            AAAAMeshletVertex PullVertexChecked(const AAAAMeshlet meshlet, const uint index)
            {
                if (index == -1)
                {
                    return (AAAAMeshletVertex) 0;
                }
                return PullVertex(meshlet, index);
            }

            Varyings VS(const uint svInstanceID : SV_InstanceID, const uint svIndexID : SV_VertexID)
            {
                InitIndirectDrawArgs(0);
                
                Varyings OUT;

                const AAAAMeshletRenderRequest meshletRenderRequest = PullMeshletRenderRequest(_MeshletRenderRequests, GetIndirectInstanceID_Base(svInstanceID));
                const uint indexID = GetIndirectVertexID_Base(svIndexID);

                const AAAAInstanceData perInstanceData = PullInstanceData(meshletRenderRequest.InstanceID);

                const AAAAMeshlet       meshlet = PullMeshletData(meshletRenderRequest.MeshletID);
                const uint              index = PullIndexChecked(meshlet, indexID);
                const AAAAMeshletVertex vertex = PullVertexChecked(meshlet, index);

                
                const float3 positionWS = mul(perInstanceData.ObjectToWorldMatrix, float4(vertex.Position.xyz, 1.0f)).xyz;

                OUT.positionCS = TransformWorldToHClip(positionWS);

                VisibilityBufferValue visibilityBufferValue;
                visibilityBufferValue.instanceID = meshletRenderRequest.InstanceID;
                visibilityBufferValue.meshletID = meshletRenderRequest.MeshletID;
                visibilityBufferValue.indexID = indexID;
                OUT.visibilityValue = PackVisibilityBufferValue(visibilityBufferValue);

                return OUT;
            }

            uint2 PS(const Varyings IN) : SV_TARGET
            {
                return IN.visibilityValue;
            } 
            
            ENDHLSL
        }
    }
    Fallback Off
}
