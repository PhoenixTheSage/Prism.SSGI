#ifndef SSGI2_BINDINGS
#define SSGI2_BINDINGS

#pragma pack_matrix(row_major)

SamplerState DefaultSampler : register(s0);
SamplerState PointSampler   : register(s1);
SamplerState LinearSampler  : register(s2);

cbuffer Constants : register(b0)
{
    float4x4 ViewMatrix;
    float4x4 PrevViewMatrix;

    float2 ScreenSize;
    float2 SceneSize;
    float MaxHistory;
    int AtrousStepSize;

    float Farplane;
    float _pad0;
    float2 _pad1;
};

Texture2D<float4> GBuffer0 : register(t0);
Texture2D<float4> GBuffer1 : register(t1);
Texture2D<float4> GBuffer2 : register(t2);
Texture2D<float> LinearDepth : register(t4);

float3 UnpackNormal(float2 packed)
{
    float2 fenc = mad(packed, 4, -2);
    float f = min(dot(fenc, fenc), 4);
    float g = sqrt(saturate(1 - f / 4));
    return float3(fenc * g, 1 - f / 2);
}

float3 LoadViewNormal(uint2 pixel)
{
    float2 uv = (float2(pixel) + 0.5) / max(ScreenSize, 1);
    return UnpackNormal(GBuffer1[uint2(uv * SceneSize)].xy);
}

float LoadWorldDepth(uint2 pixel)
{
    float2 uv = (float2(pixel) + 0.5) / max(ScreenSize, 1);
    return LinearDepth[uint2(uv * SceneSize)];
}

#endif
