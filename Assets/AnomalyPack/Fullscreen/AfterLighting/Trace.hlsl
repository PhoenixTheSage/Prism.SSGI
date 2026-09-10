#include <AnomalyFullscreen.hlsli>

#define HalfProjScale   AnomalyPassUniform0.x
#define GIIntensity     AnomalyPassUniform0.y
#define AOIntensity     AnomalyPassUniform0.z
#define SliceCount      ((int)AnomalyPassUniform0.w)
#define StepCount       ((uint)AnomalyPassUniform1.x)
#define Radius          AnomalyPassUniform1.y
#define ExpFactor       AnomalyPassUniform1.z
#define Thickness       AnomalyPassUniform1.w
#define MipLevel        AnomalyPassUniform2.x
#define JitterSamples   AnomalyPassUniform2.y
#define FrameIndex      ((uint)AnomalyPassUniform2.z)
#define ScreenSize      AnomalyPassUniform3.xy
#define Proj11          AnomalyPassUniform3.z
#define Proj22          AnomalyPassUniform3.w
#define Proj31          AnomalyPassUniform4.x
#define Proj32          AnomalyPassUniform4.y
#define Farplane        AnomalyPassUniform5.x

static const float SSGI_PI = 3.141592653589793;
static const float SSGI_HALF_PI = 1.5707963267948966;
static const float2 InvScreenSize = 1.0 / ScreenSize;
static const uint SectorCount = 32;
static const float InvSectorCount = 1.0 / float(SectorCount);

bool IsForeground(float linearDepth)
{
    return linearDepth > 1e-4 && linearDepth < Farplane * 0.999;
}

float SpatialOffsets(int2 position)
{
    return 0.25 * float((position.y - position.x) & 3);
}

float rand(float2 uv)
{
    float a = 12.9898;
    float b = 78.233;
    float c = 43758.5453;
    float dt = dot(uv.xy, float2(a, b));
    float sn = fmod(dt, 3.14);
    return frac(sin(sn) * c);
}

float2 GTAOFastAcos(float2 x)
{
    float2 outVal = -0.156583 * abs(x) + SSGI_HALF_PI;
    outVal *= sqrt(1.0 - abs(x));
    return x >= 0 ? outVal : SSGI_PI - outVal;
}

float GradientNoise(int2 position)
{
    return frac(52.9829189 * frac(dot(position, float2(0.06711056, 0.00583715))));
}

float3 UnpackNormal(float2 packed)
{
    float2 fenc = mad(packed, 4, -2);
    float f = min(dot(fenc, fenc), 4);
    float g = sqrt(saturate(1 - f / 4));
    return float3(fenc * g, 1 - f / 2);
}

float3 LoadViewNormal(uint2 pixel)
{
    return UnpackNormal(AnomalyGBuffer1[pixel].xy);
}

float3 ComputeScreenRay(float2 uv)
{
    const float ray_x = 1. / Proj11;
    const float ray_y = 1. / Proj22;
    float3 projOffset = float3(Proj31 / Proj11, Proj32 / Proj22, 0);
    return projOffset + float3(lerp(-ray_x, ray_x, uv.x), -lerp(-ray_y, ray_y, uv.y), -1.0);
}

float3 ReconstructViewPosition(float linearDepth, float2 uv)
{
    return linearDepth * ComputeScreenRay(uv);
}

float3 SampleLit(float2 uv, float mip)
{
    if (mip > 0.5)
        return AnomalyPackSrv0.SampleLevel(AnomalyPointSampler, uv, mip).xyz;
    return AnomalySceneColor.SampleLevel(AnomalyPointSampler, uv, 0).xyz;
}

float3 HorizonAngle(float3 viewPosition, float3 viewDir, float3 viewNormal, float3 projNormal, float2 rayOrigin, float2 rayDir, float stepSize, float initialStep, bool directionIsRight, float N, inout uint occludedSectors)
{
    float radius = stepSize * max(1, StepCount - 1);
    float samplingDirection = directionIsRight ? 1 : -1;

    rayDir *= samplingDirection;

    float3 light = 0;
    for (uint i = 0; i < StepCount; i++)
    {
        float rayOffset = pow(abs(stepSize * (i + initialStep)) / radius, ExpFactor) * radius;
        float2 rayPosUV = rayOrigin + ((rayDir * InvScreenSize) * max(rayOffset, i + 1));

        if (any(saturate(rayPosUV) != rayPosUV))
            break;

        float rayHitDepth = AnomalyLinearDepth.SampleLevel(AnomalyPointSampler, rayPosUV, 0);
        float3 rayHitPos = ReconstructViewPosition(rayHitDepth, rayPosUV);
        float3 rayHitPosBack = rayHitPos + (-viewDir * Thickness);

        float3 rayHitDir = normalize(rayHitPos - viewPosition);
        float3 rayHitDirBack = normalize(rayHitPosBack - viewPosition);

        float2 frontBackHorizon = float2(dot(rayHitDir, viewDir), dot(rayHitDirBack, viewDir));
        frontBackHorizon = GTAOFastAcos(clamp(frontBackHorizon, -1, 1));
        frontBackHorizon = saturate(((samplingDirection * -frontBackHorizon) - N + SSGI_HALF_PI) / SSGI_PI);
        frontBackHorizon = directionIsRight ? frontBackHorizon.yx : frontBackHorizon.xy;

        uint startSector = frontBackHorizon.x * 32;
        uint sectorCount = round((frontBackHorizon.y - frontBackHorizon.x) * 32);

        uint newlyOccludedSectors = sectorCount > 0 ? ((0xFFFFFFFFu >> (32 - sectorCount)) << startSector) : 0;
        newlyOccludedSectors &= ~occludedSectors;
        occludedSectors |= newlyOccludedSectors;

        if (newlyOccludedSectors > 0)
        {
            float3 rayHitColor = SampleLit(rayPosUV, MipLevel);

            float cosineTerm = saturate(dot(viewNormal, rayHitDir));

            if (any(rayHitColor > 0.001) && cosineTerm > 0.001)
            {
                float3 hitViewNormal = LoadViewNormal(rayPosUV * ScreenSize);

                float outgoingLightFactor = saturate(dot(hitViewNormal, -rayHitDir));

                light += countbits(newlyOccludedSectors) * InvSectorCount * rayHitColor * cosineTerm * (outgoingLightFactor > 0);
            }
        }
    }
    return light;
}

static const float spatialOffsets[4] = { 0, 0.5f, 0.25f, 0.75f };

#define USE_TEMPORAL_DIRECTIONS 0

float4 __pixel_shader(const float4 position : SV_Position, const float2 uv : TEXCOORD0) : SV_Target
{
    uint2 pixelPos = position.xy;

    if (!IsForeground(AnomalyLinearDepth[pixelPos]))
        return 0;

    float3 viewPosition = ReconstructViewPosition(AnomalyLinearDepth[pixelPos], uv) * 0.999;
    float3 viewNormal = LoadViewNormal(pixelPos);
    float3 viewDir = normalize(-viewPosition);

    float noiseDirection = GradientNoise(pixelPos);
#if USE_TEMPORAL_DIRECTIONS
    float initialStep = spatialOffsets[(FrameIndex / 6) % 4] + rand(uv) * JitterSamples;
#else
    float initialStep = spatialOffsets[FrameIndex % 4] + rand(uv) * JitterSamples;
#endif
    float stepSize = max(Radius * HalfProjScale / -viewPosition.z, StepCount) / float(StepCount + 1);

    float ambientOcclusion = 0;
    float3 light = 0;
    for (int slice = 0; slice < SliceCount; slice++)
    {
#if USE_TEMPORAL_DIRECTIONS
        float angleInRadians = SSGI_PI * (float(slice + noiseDirection) / float(SliceCount));
#else
        float angleInRadians = SSGI_PI * (float(slice + noiseDirection) / float(SliceCount));
#endif

        float2 rayDir;
        sincos(angleInRadians, rayDir.y, rayDir.x);

        float3 sliceNormal = normalize(cross(float3(rayDir.x, -rayDir.y, 0), viewDir));

        float3 projNormal = normalize(viewNormal - sliceNormal * dot(viewNormal, sliceNormal));

        float3 T = cross(viewDir, sliceNormal);

        float N = -sign(dot(projNormal, T)) * acos(clamp(dot(projNormal, viewDir), -1, 1));

        uint bitmaskLeft = 0, bitmaskRight = 0;
        light += HorizonAngle(viewPosition, viewDir, viewNormal, projNormal, uv, rayDir, stepSize, initialStep, true, N, bitmaskLeft);
        light += HorizonAngle(viewPosition, viewDir, viewNormal, projNormal, uv, rayDir, stepSize, initialStep, false, N, bitmaskRight);

        ambientOcclusion += countbits(bitmaskLeft | bitmaskRight);
    }

    ambientOcclusion = ambientOcclusion * InvSectorCount / float(SliceCount);
    ambientOcclusion = 1 - saturate(ambientOcclusion);
    ambientOcclusion = saturate(pow(ambientOcclusion, AOIntensity));

    light /= float(SliceCount);
    light *= GIIntensity;
    if (!all(isfinite(light)))
        light = 0;

    return float4(light, ambientOcclusion);
}
