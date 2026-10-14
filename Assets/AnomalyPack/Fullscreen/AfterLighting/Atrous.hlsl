#define ANOMALY_PACK_SRV0_TYPE Texture2D<float4>
#include <AnomalyFullscreen.hlsli>
#include "Denoise.hlsli"

// AnomalyPassUniform0.x is the pixel step. Three iterations use 4, 2, 1.

float4 __pixel_shader(float4 position : SV_Position, float2 uv : TEXCOORD0) : SV_Target
{
    int2 pixelPos = int2(position.xy);
    float centerDepth = SsgiDepthAt(pixelPos);
    if (!SsgiForeground(centerDepth))
        return 0;

    int stepSize = max(int(AnomalyPassUniform0.x + 0.5), 1);
    float3 centerNormal = SsgiNormalAt(pixelPos);
    float4 center = AnomalyPackSrv0[pixelPos];
    float centerLum = SsgiLuma(center.xyz);
    float phiDepth = max(centerDepth * 0.02, 1e-3) * stepSize;
    float phiLum = max(0.05 * sqrt(max(center.w, 1e-10)), 0.35 * max(centerLum, 1e-4));
    float weights[3] = { 1.0, 2.0 / 3.0, 1.0 / 6.0 };
    float totalWeight = 1;
    float3 totalColor = center.xyz;
    float totalVariance = center.w;
    float neighborLumMax = 0;

    [loop]
    for (int y = -2; y <= 2; y++)
    {
        [loop]
        for (int x = -2; x <= 2; x++)
        {
            if (x == 0 && y == 0)
                continue;
            int2 samplePos = pixelPos + int2(x, y) * stepSize;
            if (any(samplePos < 0 || samplePos >= int2(AnomalyPassSize)))
                continue;
            float depth = SsgiDepthAt(samplePos);
            if (!SsgiForeground(depth))
                continue;
            float4 sampleColor = AnomalyPackSrv0[samplePos];
            float lum = SsgiLuma(sampleColor.xyz);
            float3 normal = SsgiNormalAt(samplePos);
            float kernel = weights[abs(x)] * weights[abs(y)];
            float wNormal = pow(saturate(dot(centerNormal, normal)), 128.0);
            float wDepth = abs(centerDepth - depth) / phiDepth;
            float wLum = abs(centerLum - lum) / max(phiLum, 1e-4);
            float edge = exp(-wLum - wDepth) * wNormal;
            float weight = kernel * edge;
            totalWeight += weight;
            totalColor += sampleColor.xyz * weight;
            totalVariance += sampleColor.w * weight * weight;
            neighborLumMax = max(neighborLumMax, lum);
        }
    }

    if (neighborLumMax > 0 && centerLum > 12.0 * neighborLumMax && totalWeight > 1)
    {
        totalColor -= center.xyz;
        totalWeight -= 1;
        totalVariance -= center.w;
    }
    if (totalWeight <= 1e-4)
        return float4(0, 0, 0, 0);
    float3 finalColor = totalColor / totalWeight;
    float finalVariance = totalVariance / (totalWeight * totalWeight);
    if (!all(isfinite(finalColor)))
        finalColor = 0;
    return float4(finalColor, max(finalVariance, 0));
}
