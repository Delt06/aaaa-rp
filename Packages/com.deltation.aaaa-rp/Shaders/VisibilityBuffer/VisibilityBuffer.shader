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
                
                Varyings OUT = (Varyings) 0;

                const uint instanceID = GetIndirectInstanceID_Base(svInstanceID);
                const uint rawIndexID = GetIndirectVertexID_Base(svIndexID);

                const uint meshletID = rawIndexID / MAX_MESHLET_INDICES;

                const AAAAMeshlet meshlet = PullMeshletData(meshletID);
                const uint indexID = rawIndexID % MAX_MESHLET_INDICES;
                const uint index = PullIndexChecked(meshlet, indexID);
                const AAAAMeshletVertex vertex = PullVertexChecked(meshlet, index);

                const AAAAInstanceData perInstanceData = PullInstanceData(instanceID);
                const float3 positionWS = mul(perInstanceData.ObjectToWorldMatrix, float4(vertex.Position.xyz, 1.0f)).xyz;

                OUT.positionCS = TransformWorldToHClip(positionWS);

                VisibilityBufferValue visibilityBufferValue;
                visibilityBufferValue.instanceID = instanceID;
                visibilityBufferValue.meshletID = meshletID;
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
