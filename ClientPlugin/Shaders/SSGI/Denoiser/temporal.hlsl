#include "common.hlsli"

Texture2D<float4> History      : register(t5);
Texture2D<float3> Source       : register(t6);
Texture2D<float2> velocityTex  : register(t7);
Texture2D<float> prevDepthTex  : register(t8);
Texture2D<float4> prevGBuffer1 : register(t9);
#if VARIANCE_GUIDED
Texture2D<float4> prevMomentsAndHistoryLength : register(t10);
#endif

float3 ReprojectPrevViewNormal(float3 prevViewNormal)
{
    float3 prevWorldNormal = mul((float3x3) PrevViewMatrix, prevViewNormal);
    float3x3 invView = transpose((float3x3) ViewMatrix);
    return mul(invView, prevWorldNormal);
}

float4 ps(const float4 position : SV_Position, const float2 uv : TEXCOORD, out float3 momentsAndHistoryLength : SV_Target1) : SV_Target0
{
    momentsAndHistoryLength = 0;

#if VISUALIZE_MOTION
    return float4(abs(velocityTex[position.xy]) * 500 * isfinite(velocityTex[position.xy]), 0, 1);
#endif

#if !ENABLE_TEMPORAL
    return float4(Source[position.xy].xyz, 1);
#endif

    const uint2 pixelPos = position.xy;
    const float linearZ = LinearDepth[pixelPos];
    if (!IsForeground(linearZ))
    {
        return 0;
    }

    const float2 motion = velocityTex[pixelPos];
    const float2 prevPosF = pixelPos + motion;

    float2 xyf = frac(prevPosF);

    const int2 offsets[4] = { int2(0, 0), int2(1, 0), int2(0, 1), int2(1, 1) };
    const float weights[4] = { (1 - xyf.x) * (1 - xyf.y), xyf.x * (1 - xyf.y), (1 - xyf.x) * xyf.y, xyf.x * xyf.y };

    float3 viewNormal = LoadViewNormal(pixelPos);

    float weightSum = 0;
    float4 historySum = 0;
    for (uint i = 0; i < 4; i++)
    {
        int2 offsetPos = prevPosF + offsets[i];
        if (any(offsetPos < 0 || offsetPos >= ScreenSize))
            continue;

        float prevLinear = prevDepthTex[offsetPos];
        if (!IsForeground(prevLinear))
            continue;

        float depthDiff = abs(linearZ - prevLinear);

        float3 prevViewNormalReproj = ReprojectPrevViewNormal(UnpackNormal(prevGBuffer1[offsetPos].xy));

        static const float DEPTH_DIFF_THRESHOLD = 0.05;
        static const float NORMAL_DOT_DIFF_THRESHOLD = 0.5;

        bool depthRejected = depthDiff > DEPTH_DIFF_THRESHOLD;
        bool normalRejected = dot(prevViewNormalReproj, viewNormal) < NORMAL_DOT_DIFF_THRESHOLD;
        if (depthRejected || normalRejected)
            continue;

        weightSum += weights[i];
#if !VARIANCE_GUIDED
        historySum += weights[i] * History[offsetPos];
#else
        historySum += weights[i] * float4(History[offsetPos].xyz, prevMomentsAndHistoryLength[offsetPos].z);
#endif
    }

    float4 history = weightSum > 0 ? (historySum / weightSum) : 0;
    history.w = clamp(history.w, 0, MaxHistory);
    history.w += 1.0;

    float mip = max(0, -(history.w - min(4, MaxHistory)));
    float3 currentColor;
    [branch]
    if (mip >= 1)
    {
        currentColor = 0;
        int radius = 1 << int(mip - 1);
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                int2 offsetPos = pixelPos + int2(x, y);
                if (any(offsetPos < 0 || offsetPos >= ScreenSize))
                    continue;

                static const float DEPTH_DIFF_THRESHOLD = 0.1;

                float depthDiff = abs(linearZ - LoadWorldDepth(offsetPos));
                if (depthDiff > DEPTH_DIFF_THRESHOLD)
                    continue;

                currentColor += Source[offsetPos];
            }
        }
        currentColor /= sq(radius * 2 + 1);
    }
    else
    {
        currentColor = Source.SampleLevel(PointSampler, uv, 0);
    }

    float alpha = 1.0 / history.w;

    float3 finalColor = lerp(history.xyz, currentColor, alpha);
    finalColor = isfinite(finalColor) ? finalColor : 0;

#if !VARIANCE_GUIDED
    return float4(finalColor, history.w);
#else

#if VISUALIZE_HISTORY_LENGTH
    momentsAndHistoryLength = history.w;
    return history.w;
#endif

    float2 moments;
    moments.x = luminance(currentColor);
    moments.y = sq(moments.x);
    moments = lerp(prevMomentsAndHistoryLength[pixelPos].xy, moments, alpha);
    momentsAndHistoryLength = float3(moments, history.w);
    float variance = abs(momentsAndHistoryLength.y - sq(momentsAndHistoryLength.x)) + 0.05;
    return float4(finalColor, variance);
#endif
}
