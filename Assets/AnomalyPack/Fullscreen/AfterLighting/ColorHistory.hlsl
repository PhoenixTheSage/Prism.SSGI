#define ANOMALY_PACK_SRV0_TYPE Texture2D<float4>
#include <AnomalyFullscreen.hlsli>

// Preserve the final filtered radiance for the next temporal pass, as DX11 does.
float4 __pixel_shader(float4 position : SV_Position, float2 uv : TEXCOORD0) : SV_Target
{
    return AnomalyPackSrv0[uint2(position.xy)];
}
