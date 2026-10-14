#define ANOMALY_PACK_SRV0_TYPE Texture2D<float4>
#include <AnomalyFullscreen.hlsli>
#include "Denoise.hlsli"

// DirectAdd into world HDR. Albedo and metalness match the DX11 copy into LBuffer.

float4 __pixel_shader(float4 position : SV_Position, float2 uv : TEXCOORD0) : SV_Target
{
    if (!SsgiForeground(AnomalyLinearDepth[uint2(position.xy)]))
        return 0;
    float3 gi = AnomalyPackSrv0.SampleLevel(AnomalyLinearSampler, uv, 0).rgb;
    float3 albedo = AnomalyGBuffer0[uint2(position.xy)].rgb;
    float metal = AnomalyGBuffer2[uint2(position.xy)].x;
    float3 color = gi * albedo * (1 - saturate(metal));
    return all(isfinite(color)) ? float4(color, 0) : 0;
}
