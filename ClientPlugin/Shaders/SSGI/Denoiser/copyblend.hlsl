#include "common.hlsli"

Texture2D<float3> Input : register(t5);

float3 ApplyBlending(const uint2 pixelPos, float3 color)
{
#if BLEND_WITH_ALBEDO
    color *= GBuffer0[pixelPos].xyz;
#endif
#if BLEND_WITH_METALNESS
    color *= (1 - GBuffer2[pixelPos].x);
#endif
    return color;
}

float3 ps(const float4 position : SV_Position, const float2 uv : TEXCOORD) : SV_Target
{
    const int2 pixelPos = position.xy;

    if (!IsForeground(LinearDepth[pixelPos]))
    {
        return 0;
    }

    float3 color = ApplyBlending(pixelPos, Input.SampleLevel(LinearSampler, uv, 0));
    return all(isfinite(color)) ? color : 0;
}
