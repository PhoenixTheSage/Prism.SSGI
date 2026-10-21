#define ANOMALY_PACK_SRV0_TYPE Texture2D<float4>
#define ANOMALY_PACK_SRV1_TYPE Texture2D<float4>
#define ANOMALY_PACK_SRV2_TYPE Texture2D<float4>
#define ANOMALY_PACK_SRV3_TYPE Texture2D<float4>
#define ANOMALY_PACK_SRV4_TYPE Texture2D<float4>
#include <AnomalyFullscreen.hlsli>
#include "Denoise.hlsli"

// Same reprojection as Temporal.hlsl. The guest bus has one color target,
// so moments and history length are this second pass. .xy luma moments,
// .z history length, .w variance.

float4 __pixel_shader(float4 position : SV_Position, float2 uv : TEXCOORD0) : SV_Target
{
    uint2 pixelPos = uint2(position.xy);
    float linearZ = SsgiDepthAt(int2(pixelPos));
    if (!SsgiForeground(linearZ))
        return 0;

    float maxHistory = max(AnomalyPassUniform0.x, 0.0);
    bool skipNormal = AnomalyPassUniform0.y > 0.5;
    uint2 scenePixel = SsgiScenePixel(pixelPos);
    float2 motionPass = AnomalyVelocityBuffer[scenePixel] * (AnomalyPassSize / max(AnomalySceneSize, float2(1, 1)));
    float2 prevPos = float2(pixelPos) + motionPass;
    int2 prevBase = int2(floor(prevPos));
    float2 fracPos = frac(prevPos);
    int2 offsets[4] = { int2(0, 0), int2(1, 0), int2(0, 1), int2(1, 1) };
    float weights[4] = {
        (1 - fracPos.x) * (1 - fracPos.y), fracPos.x * (1 - fracPos.y),
        (1 - fracPos.x) * fracPos.y, fracPos.x * fracPos.y
    };
    float3 viewNormal = normalize(AnomalyViewToWorldAt0(SsgiNormalAt(int2(pixelPos))));
    float weightSum = 0;
    float historyLengthSum = 0;
    float2 momentSum = 0;
    [unroll]
    for (uint i = 0; i < 4; i++)
    {
        int2 samplePos = prevBase + offsets[i];
        if (any(samplePos < 0 || samplePos >= int2(AnomalyPassSize)))
            continue;
        float prevDepth = AnomalyPackSrv2[samplePos].x;
        if (!SsgiForeground(prevDepth))
            continue;
        float rel = abs(linearZ - prevDepth) / max(max(linearZ, prevDepth), 1e-2);
        if (rel > 0.15 && abs(linearZ - prevDepth) > 0.05)
            continue;
        if (!skipNormal)
        {
            float3 previous = AnomalyPackSrv3[samplePos].xyz;
            if (dot(previous, viewNormal) < 0.2)
                continue;
        }
        float4 moments = AnomalyPackSrv4[samplePos];
        weightSum += weights[i];
        historyLengthSum += weights[i] * moments.z;
        momentSum += weights[i] * moments.xy;
    }

    float historyLength = 1;
    float2 previousMoments = 0;
    if (weightSum > 0)
    {
        previousMoments = momentSum / weightSum;
        historyLength = clamp(historyLengthSum / weightSum, 0, maxHistory) + 1;
    }

    float3 currentColor = AnomalyPackSrv1[pixelPos].xyz;
    float neighMax = 0;
    [unroll]
    for (int ny = -1; ny <= 1; ny++)
    {
        [unroll]
        for (int nx = -1; nx <= 1; nx++)
        {
            if (nx == 0 && ny == 0)
                continue;
            int2 npos = int2(pixelPos) + int2(nx, ny);
            if (any(npos < 0 || npos >= int2(AnomalyPassSize)))
                continue;
            neighMax = max(neighMax, SsgiLuma(AnomalyPackSrv1[npos].xyz));
        }
    }
    currentColor = SsgiClampFirefly(currentColor, neighMax);
    float luma = SsgiLuma(currentColor);
    float alpha = 1.0 / historyLength;
    float2 moments = lerp(previousMoments, float2(luma, luma * luma), alpha);
    float variance = abs(moments.y - moments.x * moments.x);
    variance = max(variance, (luma * luma) / max(historyLength, 1.0));
    if (!all(isfinite(moments)))
        moments = 0;
    return float4(moments, historyLength, variance + 1e-4);
}
