#include "../../ClientPlugin/Shaders/SSGI/Denoiser/temporal.hlsl"

RWStructuredBuffer<float4> Output : register(u0);
[numthreads(1, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    float3 moments;
    Output[id.y * 3 + id.x] = ps(float4(float2(id.xy) + 0.5, 0, 1), (float2(id.xy) + 0.5) / 3, moments);
}
