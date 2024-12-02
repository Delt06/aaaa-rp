#ifndef AAAA_META_INPUT_INCLUDED
#define AAAA_META_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/MetaPass.hlsl"

#define MetaInput UnityMetaInput
#define MetaFragment UnityMetaFragment

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv0 : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    float2 uv2 : TEXCOORD2;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    
    float2 uv : TEXCOORD0;
    #ifdef EDITOR_VISUALIZATION
    float2 VizUV        : TEXCOORD1;
    float4 LightCoord   : TEXCOORD2;
    #endif
};

Varyings MetaVS(Attributes input)
{
    Varyings output = (Varyings)0;
    output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv1, input.uv2);
    output.uv = input.uv0;
    #ifdef EDITOR_VISUALIZATION
    UnityEditorVizData(input.positionOS.xyz, input.uv0, input.uv1, input.uv2, output.VizUV, output.LightCoord);
    #endif
    return output;
}

float4 MetaPS(Varyings input, MetaInput metaInput)
{
    #ifdef EDITOR_VISUALIZATION
    metaInput.VizUV = input.VizUV;
    metaInput.LightCoord = input.LightCoord;
    #endif

    return UnityMetaFragment(metaInput);
}

#endif // AAAA_META_INPUT_INCLUDED