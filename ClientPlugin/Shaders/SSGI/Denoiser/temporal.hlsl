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
    // ViewAt0 3x3 is orthonormal (camera-at-origin). Row-major world-to-view.
    float3 worldN = mul(prevViewNormal, transpose((float3x3)PrevViewMatrix));
    return mul(worldN, (float3x3)ViewMatrix);
}

uint2 ScenePixelFromPass(uint2 passPixel)
{
    float2 uv = (float2(passPixel) + 0.5) / max(ScreenSize, 1);
    return min(uint2(uv * SceneSize), uint2(max(SceneSize, 1)) - 1);
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
    const uint2 scenePixel = ScenePixelFromPass(pixelPos);
    const float linearZ = LinearDepth[scenePixel];
    if (!IsForeground(linearZ))
    {
        return 0;
    }

    // Catalog velocity: full-res pixel delta, previousPixel = currentPixel + mv.
    const float2 motionPass = velocityTex[scenePixel] * (ScreenSize / max(SceneSize, 1));
    const float2 prevPosF = float2(pixelPos) + motionPass;
    const int2 prevBase = int2(floor(prevPosF));
    const float2 xyf = frac(prevPosF);

    const int2 offsets[4] = { int2(0, 0), int2(1, 0), int2(0, 1), int2(1, 1) };
    const float weights[4] = { (1 - xyf.x) * (1 - xyf.y), xyf.x * (1 - xyf.y), (1 - xyf.x) * xyf.y, xyf.x * xyf.y };

    float3 viewNormal = LoadViewNormal(pixelPos);

    float weightSum = 0;
    float4 historySum = 0;
#if VARIANCE_GUIDED
    float2 momentSum = 0;
#endif
    [unroll]
    for (uint i = 0; i < 4; i++)
    {
        int2 offsetPos = prevBase + offsets[i];
        if (any(offsetPos < 0 || offsetPos >= int2(ScreenSize)))
            continue;

        float prevLinear = prevDepthTex[offsetPos];
        if (!IsForeground(prevLinear))
            continue;

        // Linear depth is meters. 5 cm absolute rejects a walk cycle.
        float relDepth = abs(linearZ - prevLinear) / max(max(linearZ, prevLinear), 1e-2);
        if (relDepth > 0.15)
            continue;

        float3 prevViewNormalReproj = ReprojectPrevViewNormal(UnpackNormal(prevGBuffer1[offsetPos].xy));
        if (dot(prevViewNormalReproj, viewNormal) < 0.2)
            continue;

        weightSum += weights[i];
#if !VARIANCE_GUIDED
        historySum += weights[i] * History[offsetPos];
#else
        float4 prevMom = prevMomentsAndHistoryLength[offsetPos];
        historySum += weights[i] * float4(History[offsetPos].xyz, prevMom.z);
        momentSum += weights[i] * prevMom.xy;
#endif
    }

    float4 history = weightSum > 0 ? (historySum / weightSum) : 0;
#if VARIANCE_GUIDED
    float2 prevMoments = weightSum > 0 ? (momentSum / weightSum) : 0;
#endif
    history.w = clamp(history.w, 0, MaxHistory);
    history.w += 1.0;

    float3 currentColor = Source[pixelPos];

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
    moments = lerp(prevMoments, moments, alpha);
    momentsAndHistoryLength = float3(moments, history.w);
    float variance = abs(moments.y - sq(moments.x));
    // One SSILVB sample has ~0 empirical variance, so à-trous will not smear
    // the hits. Inflate until temporal history actually fills the pixel.
    float lum = max(moments.x, 1e-4);
    variance = max(variance, sq(lum) / max(history.w, 1.0));
    return float4(finalColor, variance + 1e-4);
#endif
}
