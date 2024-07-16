Shader "Hidden/AAAA/VisibilityBufferDebug"
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
            ZTest Off
            ZClip Off
            Cull Off
            
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/GBuffer.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Barycentric.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Instances.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Meshlets.hlsl"
            #include "Packages/com.deltation.aaaa-rp/ShaderLibrary/VisibilityBuffer/Utils.hlsl"
            #include "Packages/com.deltation.aaaa-rp/Runtime/Debugging/AAAADebugDisplaySettingsRendering.cs.hlsl"

            #define DIM 0.1
            #define BRIGHT 0.8
            #define COLORS_COUNT 6
            
            static const float3 colors[COLORS_COUNT] =
            {
                float3(BRIGHT, BRIGHT, DIM),  
                float3(BRIGHT, DIM,    BRIGHT),  
                float3(BRIGHT, DIM,    DIM),  
                float3(DIM,    BRIGHT, BRIGHT),  
                float3(DIM,    BRIGHT, DIM),  
                float3(DIM,    DIM,    BRIGHT),  
            };

            float3 GetColor(const uint index)
            {
                return colors[index % COLORS_COUNT];
            }

            uint _VisibilityBufferDebugMode;

            float InverseLerpUnclamped(const float a, const float b, const float v)
            {
                #pragma warning (disable : 4008) // Suppress the division by zero warning
                return (v - a) / (b - a);
                #pragma warning (restore : 4008)
            }

            float InverseLerpClamped(const float a, const float b, const float v)
            {
                return saturate(InverseLerpUnclamped(a, b, v));
            }
            
            // https://www.ronja-tutorials.com/post/046-fwidth/
            float StepAntiAliased(const float edge, const float value, const float fwidthValue)
            {
                const float halfChange = fwidthValue * 0.5f;
                return InverseLerpClamped(edge - halfChange, edge + halfChange, value);
            }

            float4 Frag(const Varyings IN) : SV_Target
            {
                const uint2 visibilityBufferPacked = SampleVisibilityBuffer(IN.texcoord);
                if (visibilityBufferPacked.x == -1)
                {
                    return 0;
                }
                
                const VisibilityBufferValue value = UnpackVisibilityBufferValue(visibilityBufferPacked);
                const AAAAInstanceData instanceData = PullInstanceData(value.instanceID);
                const uint meshletID = value.meshletID;

                const AAAAMeshlet meshlet = PullMeshletData(meshletID);
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

                switch (_VisibilityBufferDebugMode)
                {
                case AAAAVISIBILITYBUFFERDEBUGMODE_WIREFRAME:
                    {
                        const float baryMinValue = min(barycentric.lambda.x, min(barycentric.lambda.y, barycentric.lambda.z));
                        const float threshold = _ScreenSize.z * 10;
                        const float minBary = smoothstep(threshold, threshold + 0.01, baryMinValue);
                        return lerp(float4(GetColor(meshletID), 1), 0, minBary);
                    }
                case AAAAVISIBILITYBUFFERDEBUGMODE_BARYCENTRIC_COORDINATES:
                    {
                        return float4(barycentric.lambda, 1.0f);
                    }
                case AAAAVISIBILITYBUFFERDEBUGMODE_INSTANCE_ID:
                    {
                        return float4(GetColor(value.instanceID), 1);
                    }
                case AAAAVISIBILITYBUFFERDEBUGMODE_MESHLET_ID:
                    {
                        return float4(GetColor(meshletID), 1);
                    }
                case AAAAVISIBILITYBUFFERDEBUGMODE_INDEX_ID:
                    {
                        return float4(GetColor(value.indexID / 3), 1);
                    }
                case AAAAVISIBILITYBUFFERDEBUGMODE_MESH_LOD:
                    {
                        return float4(GetColor(value.meshLOD), 1);
                    }
                default:
                    {
                        return 0;   
                    }
                }
            }
            ENDHLSL
        }
    }

    Fallback Off
}
