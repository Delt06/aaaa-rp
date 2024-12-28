Shader "Hidden/AAAA/VXGI/Voxelize"
{
    Properties
    {
        _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        ZClip Off
        ZTest Off
        ZWrite Off
        ColorMask 0
        Cull Off

        Pass
        {
            Name "VXGI: Voxelize"

            HLSLPROGRAM

            #pragma require geometry

            #pragma vertex VoxelizeVS
            #pragma geometry VoxelizeGS
            #pragma fragment VoxelizePS

            #include_with_pragmas "Packages/com.deltation.aaaa-rp/ShaderLibrary/Bindless.hlsl"

            #pragma multi_compile_local _ _ALPHATEST_ON

            #include "Packages/com.deltation.aaaa-rp/Shaders/GlobalIllumination/VXGI/VoxelizePass.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}