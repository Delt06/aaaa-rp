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
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Utils.hlsl"

            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawArgs
            #include "UnityIndirect.cginc"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation uint2 visibilityValue : VISIBILITY_VALUE;
            };

            static float3 vertices[] = 
            {
                float3(0, 0, 0),
                float3(0, 1, 0),
                float3(1, 1, 0),
                float3(1, 0, 0),
            };

            #define INDEX_BUFFER_SIZE 6

            static uint indices[] =
                {
                    0, 1, 2,
                    2, 3, 0,
                };

            float3 LoadPosition(const uint triangleID)
            {
                const uint vertexID = indices[triangleID];
                const float3 positionOs = vertices[vertexID];
                return positionOs;
            }

            Varyings VS(const uint svInstanceID : SV_InstanceID, const uint svIndexID : SV_VertexID)
            {
                InitIndirectDrawArgs(0);
                
                Varyings OUT = (Varyings) 0;

                const uint instanceID = GetIndirectInstanceID_Base(svInstanceID);
                const uint indexID = GetIndirectVertexID_Base(svIndexID);
                
                const float3 center = float3(instanceID * 1.1, 0, 0);
                const float3 offset = LoadPosition(indexID % INDEX_BUFFER_SIZE);
                const float3 positionWS = center + offset + float3(0, indexID / INDEX_BUFFER_SIZE, 0);

                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.visibilityValue = uint2(instanceID, indexID / 3);

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
