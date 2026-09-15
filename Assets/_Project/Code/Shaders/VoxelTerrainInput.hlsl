#ifndef AETHRA_VOXEL_TERRAIN_INPUT_INCLUDED
#define AETHRA_VOXEL_TERRAIN_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Identical in every pass so the SRP Batcher can keep the material in one constant buffer.
CBUFFER_START(UnityPerMaterial)
    half4 _Tint;
    half  _Smoothness;
    half  _Metallic;
CBUFFER_END

#endif
