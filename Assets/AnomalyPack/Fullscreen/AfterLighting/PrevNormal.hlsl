#include <AnomalyFullscreen.hlsli>
#include "Denoise.hlsli"

float4 __pixel_shader(float4 position : SV_Position, float2 uv : TEXCOORD0) : SV_Target
{
    float2 size = max(AnomalyPassSize, float2(1, 1));
    float2 sampleUv = (floor(position.xy) + 0.5) / size;
    return float4(normalize(AnomalyViewToWorldAt0(SsgiNormalAt(int2(position.xy)))),1);
}
