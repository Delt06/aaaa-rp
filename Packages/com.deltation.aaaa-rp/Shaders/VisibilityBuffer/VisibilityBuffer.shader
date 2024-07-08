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

            #pragma enable_d3d11_debug_symbols

            #pragma vertex VS
            #pragma fragment PS

            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Utils.hlsl"
            #include "Packages/com.deltation.aaaa-rp/Runtime/Meshlets/AAAAMeshletCollection.cs.hlsl"

            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawArgs
            #include "UnityIndirect.cginc"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation uint2 visibilityValue : VISIBILITY_VALUE;
            };

            uint _MeshletCount;
            StructuredBuffer<AAAAMeshlet> _Meshlets;
            StructuredBuffer<AAAAMeshletVertex> _SharedVertexBuffer;
            ByteAddressBuffer _SharedIndexBuffer;
            StructuredBuffer<float4x4> _ObjectToWorldMatrices;

            uint PullIndex(const AAAAMeshlet meshlet, const uint indexID)
            {
                if (indexID >= meshlet.TriangleCount * 3)
                {
                    return -1;
                }
                const uint absoluteIndexID = meshlet.TriangleOffset + indexID;
                const uint indices = _SharedIndexBuffer.Load(absoluteIndexID / 4 * 4);
                const uint shiftAmount = absoluteIndexID % 4 * 8;
                const uint mask = 0xFFu << shiftAmount;
                return (indices & mask) >> shiftAmount;
            }

            AAAAMeshletVertex PullVertex(const AAAAMeshlet meshlet, const uint index)
            {
                if (index == -1)
                {
                    return (AAAAMeshletVertex) 0;
                }
                
                return _SharedVertexBuffer[meshlet.VertexOffset + index];
            }

            Varyings VS(const uint svInstanceID : SV_InstanceID, const uint svIndexID : SV_VertexID)
            {
                InitIndirectDrawArgs(0);
                
                Varyings OUT = (Varyings) 0;

                const uint instanceID = GetIndirectInstanceID_Base(svInstanceID);
                const uint rawIndexID = GetIndirectVertexID_Base(svIndexID);

                const uint meshletID = rawIndexID / MAX_MESHLET_INDICES;

                const AAAAMeshlet meshlet = _Meshlets[meshletID];
                const uint indexID = rawIndexID % MAX_MESHLET_INDICES;
                const uint index = PullIndex(meshlet, indexID);
                const AAAAMeshletVertex vertex = PullVertex(meshlet, index);

                const float4x4 objectToWorldMatrix = _ObjectToWorldMatrices[instanceID];
                const float3 positionWS = mul(objectToWorldMatrix, float4(vertex.Position.xyz, 1.0f)).xyz;

                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.visibilityValue = PackVisibilityBufferValue(instanceID, meshletID, indexID);

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
