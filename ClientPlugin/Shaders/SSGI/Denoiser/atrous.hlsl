#include "common.hlsli"

Texture2D<float4> ColorAndVariance : register(t5);

float ComputeFilteredVariance(const int2 centerPixelPos)
{
    float sum = 0.f;

    const float kernel[2][2] =
    {
        { 1.0 / 4.0, 1.0 / 8.0 },
        { 1.0 / 8.0, 1.0 / 16.0 },
    };

    const int radius = 1;
    for (int yy = -radius; yy <= radius; yy++)
    {
        for (int xx = -radius; xx <= radius; xx++)
        {
            const int2 pos = centerPixelPos + int2(xx, yy);
            const float weight = kernel[abs(xx)][abs(yy)];
            sum += ColorAndVariance[pos].w * weight;
        }
    }

    return sum;
}

float ComputeEdgeWeight(
    float centerDepth, float depth, float phiDepth,
    float3 centerNormal, float3 normal, float phiNormal,
    float centerLuminance, float luminance, float phiLuminance)
{
    float wNormal = pow(saturate(dot(centerNormal, normal)), phiNormal);
    float wDepth = (phiDepth == 0) ? 0 : (abs(centerDepth - depth) / phiDepth);
    float wLuminance = abs(centerLuminance - luminance) / phiLuminance;

    wDepth = max(0, wDepth);
    wLuminance = max(0, wLuminance);

    float edgeWeight = exp(0 - wLuminance - wDepth) * wNormal;
    return edgeWeight;
}

static const float weights[3] = { 1, 2.0 / 3.0, 1.0 / 6.0 };

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

float4 ps(const float4 position : SV_Position, const float2 uv : TEXCOORD
#if ENABLE_BLENDED_OUTPUT
, out float3 blendedColor : SV_Target1
#endif
) : SV_Target0
{
    const int2 pixelPos = position.xy;

#if VISUALIZE_HISTORY_LENGTH
#if ENABLE_BLENDED_OUTPUT
    blendedColor = float4(ColorAndVariance[pixelPos].www / MaxHistory * 50, 1);
#endif
    return ColorAndVariance[pixelPos];
#endif

#if VISUALIZE_MOTION
#if ENABLE_BLENDED_OUTPUT
    blendedColor = ColorAndVariance[pixelPos].xyz;
#endif
    return ColorAndVariance[pixelPos];
#endif

    if (!IsForeground(LoadWorldDepth(pixelPos)))
    {
#if ENABLE_BLENDED_OUTPUT
        blendedColor = 0;
#endif
        return 0;
    }

    const float centerDepth = LoadWorldDepth(pixelPos);
    const float3 centerNormal = LoadViewNormal(pixelPos);
    const float3 centerColor = ColorAndVariance[pixelPos].xyz;
    const float centerLum = luminance(centerColor);
    const float centerVariance = ComputeFilteredVariance(pixelPos);

    float totalWeight = 1;
    float3 totalColor = centerColor;
    float totalVariance = centerVariance;

    float neighborLum = 0;
    float neighborLumMax = 0;

    float phiDepth = max(fwidth(centerDepth), 1e-8) * AtrousStepSize;
    float phiNormal = SIGMA_N;
    float phiIllumination = SIGMA_LUM * sqrt(max(0, centerVariance + 1e-10));
    // Sparse GI hits stay pin-sharp if luminance sigma is only the 0.05 floor.
    phiIllumination = max(phiIllumination, 0.35 * max(centerLum, 1e-4));

    static const int radius = 2;
    for (int y = -radius; y <= radius; y++)
    {
        for (int x = -radius; x <= radius; x++)
        {
            const int2 samplePos = pixelPos + int2(x, y) * AtrousStepSize;

            if (any(samplePos < 0 || samplePos >= ScreenSize) || all(samplePos == pixelPos) || !IsForeground(LoadWorldDepth(samplePos)))
                continue;

            const float depth = LoadWorldDepth(samplePos);
            const float3 normal = LoadViewNormal(samplePos);
            const float3 color = ColorAndVariance[samplePos].xyz;
            const float lum = luminance(color);
            const float variance = ColorAndVariance[samplePos].w;

            const float kernelWeight = weights[abs(x)] * weights[abs(y)];
            const float edgeWeight = ComputeEdgeWeight(
                centerDepth, depth, phiDepth * length(float2(x, y)),
                centerNormal, normal, phiNormal,
                centerLum, lum, phiIllumination);

            float weight = kernelWeight * edgeWeight;

            totalWeight += weight;
            totalColor += color * weight;
            totalVariance += variance * sq(weight);

            neighborLum += lum;
            neighborLumMax = max(neighborLumMax, lum);
        }
    }

    float3 finalColor = totalColor / totalWeight;
    float finalVariance = totalVariance / sq(totalWeight);
    if (!all(isfinite(finalColor)))
        finalColor = 0;

#if ENABLE_BLENDED_OUTPUT
    float maxLuminance = max(0, neighborLum / 24 * 2);
    float luminanceWeight = saturate(maxLuminance / max(centerLum, 1e-6));

    blendedColor = ApplyBlending(pixelPos, finalColor) * luminanceWeight;
    if (!all(isfinite(blendedColor)))
        blendedColor = 0;
#endif
    return float4(finalColor, finalVariance);
}
