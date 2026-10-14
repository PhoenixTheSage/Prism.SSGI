#include <AnomalyFullscreen.hlsli>

float4 __pixel_shader(float4 position : SV_Position, float2 uv : TEXCOORD0) : SV_Target
{
    float2 size = max(AnomalyPassSize, float2(1, 1));
    float2 sampleUv = (floor(position.xy) + 0.5) / size;
    return AnomalyGBuffer1[AnomalyScenePixel(sampleUv)];
}
