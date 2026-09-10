#include "common.hlsli"

Texture2D<float4> Source : register(t5);

float ComputeDepthWeight(const float centerDepth, const float neighborDepth, const float fwidth)
{
    // TODO: compute plane distance based on the center pixel's normal (see reblur slides)
    
    float depthDiff = abs(centerDepth - neighborDepth) /** FrameData.FarPlane*/;
    float eps_depth = 0.0001;
    
    return exp(-depthDiff / (SIGMA_Z * fwidth + eps_depth)) * (depthDiff < (0.05 * centerDepth));
}

float ComputeNormalWeight(const float3 centerNormal, const float3 neighborNormal)
{
    float d = dot(centerNormal, neighborNormal);
    return pow(saturate(d), SIGMA_N) * (d > 0.5);
}

float2 Rotate(float2 vec, float sinTheta, float cosTheta)
{
    return float2(
        vec.x * cosTheta - vec.y * sinTheta,
        vec.x * sinTheta + vec.y * cosTheta);
}

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

#define HQ_BLUR 0

float4 ps(const float4 position : SV_Position, const float2 uv : TEXCOORD, out float3 blendedColor : SV_Target1) : SV_Target0
{
    const int2 pixelPos = position.xy;
    
#if VISUALIZE_HISTORY_LENGTH
    blendedColor = float4(Source[pixelPos].www / Denoiser.MaxHistory * 50, 1);
    return Source[pixelPos];
#endif // VISUALIZE_HISTORY_LENGTH
    
#if VISUALIZE_MOTION
    blendedColor = Source[pixelPos].xyz;
    return Source[pixelPos];
#endif
    
#if !ENABLE_BLUR
    blendedColor = ApplyBlending(pixelPos, Source[pixelPos].xyz);
    return Source[pixelPos];
#endif
    
    if (!IsForeground(DepthBuffer[pixelPos]))
    {
        blendedColor = 0;
        return Source[pixelPos];
    }
    
    const float centerDepth = LoadWorldDepth(pixelPos);
    const float3 centerNormal = LoadViewNormal(pixelPos);
    
    float3 totalColor = 0;
    float totalWeight = 0;
    
    // add center color and weight
    totalColor += Source[pixelPos].xyz;
    totalWeight += 1;
    
    // randomly rotate sampling disc per pixel
    //float discRotation = GradientNoise(pixelPos) * PI * 2;
    //float2 rotSinCos;
    //sincos(discRotation, rotSinCos.x, rotSinCos.y);
    
    // 0, 1
    // 2, 3
    int spatialIndex = (pixelPos.x % 2) + ((pixelPos.y % 2) * 2);
    
    float radius = Denoiser.BlurRadius;
    int sampleCount = HQ_BLUR ? 64 : 16;
    for (int i = 0; i < sampleCount; i++)
    {
#if HQ_BLUR
        float2 offset = poissonDisk[i] * radius;
#else
        float2 offset = poissonDisk[i + (sampleCount * ((FrameIndex + spatialIndex) % (64 / sampleCount)))] * radius;
#endif
        int2 pos = int2(position.xy + offset);
        
        if (any(pos < 0 || pos >= ScreenSize) || all(pos == pixelPos) || !IsForeground(DepthBuffer[pos]))
            continue;
        
        const float depth = LoadWorldDepth(pos);
        const float3 normal = LoadViewNormal(pos);
        
        float weight = ComputeDepthWeight(centerDepth, depth, 1) * ComputeNormalWeight(centerNormal, normal);
        
        totalWeight += weight;
        totalColor += Source[pos].xyz * weight;
        
        if (weight < 0.01)
        {
            radius *= 0.75;
        }
        else
        {
            radius = clamp(radius * 2, 0, Denoiser.BlurRadius);
        }
    }
    
    float3 finalColor = totalWeight > 0 ? (totalColor / totalWeight) : 0;
    
    blendedColor = ApplyBlending(pixelPos, finalColor.xyz);
    return float4(finalColor.xyz, Source[pixelPos].w);
    //return Source[pixelPos];
}
