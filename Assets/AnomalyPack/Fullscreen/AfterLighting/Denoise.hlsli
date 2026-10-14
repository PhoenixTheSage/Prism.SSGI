#ifndef SSGI_DENOISE_HLSLI
#define SSGI_DENOISE_HLSLI

float SsgiLuma(float3 color)
{
    return dot(color, float3(0.2126, 0.7152, 0.0722));
}

float SsgiFar()
{
    return max(AnomalyCameraToWorldPad.x, 1.0);
}

bool SsgiForeground(float linearDepth)
{
    return AnomalyIsForeground(linearDepth, SsgiFar());
}

float SsgiDepthAt(int2 passPixel)
{
    float2 uv = (float2(passPixel) + 0.5) / max(AnomalyPassSize, float2(1, 1));
    return AnomalyLinearDepth[AnomalyScenePixel(uv)];
}

float3 SsgiUnpackNormal(float2 packed)
{
    float2 fenc = mad(packed, 4, -2);
    float f = min(dot(fenc, fenc), 4);
    float g = sqrt(saturate(1 - f / 4));
    return float3(fenc * g, 1 - f / 2);
}

float3 SsgiNormalAt(int2 passPixel)
{
    float2 uv = (float2(passPixel) + 0.5) / max(AnomalyPassSize, float2(1, 1));
    return SsgiUnpackNormal(AnomalyGBuffer1[AnomalyScenePixel(uv)].xy);
}

float3 SsgiClampFirefly(float3 color, float neighborLuminance)
{
    float cap = 12.0 * neighborLuminance;
    float lum = SsgiLuma(color);
    return neighborLuminance > 1e-4 && lum > cap ? color * (cap / lum) : color;
}

uint2 SsgiScenePixel(uint2 passPixel)
{
    float2 uv = (float2(passPixel) + 0.5) / max(AnomalyPassSize, float2(1, 1));
    return AnomalyScenePixel(uv);
}

#endif
