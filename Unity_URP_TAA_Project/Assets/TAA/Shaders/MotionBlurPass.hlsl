#ifndef CUSTOM_MOTION_BLUR_INCLUDED
#define CUSTOM_MOTION_BLUR_INCLUDED

#include "TemporalAAInput.hlsl"

float GetTrust(float vecPixelLen, float minPixelLen, float maxPixelLen)
{
    float range = maxPixelLen - minPixelLen;
    return clamp(vecPixelLen - minPixelLen, 0.0, range) * rcp(range);
}

half3 SampleMotionBlur(TEXTURE2D(tex), SAMPLER(samp), float2 uv, float2 vec, int step)
{
    float rand = PDsrand(uv + _SinTime.xx);
    float2 stepLen = vec * rcp(2.0 * step);
    float2 origin = uv + stepLen * rand * 0.5;

    half3 color = half3(0.0, 0.0, 0.0);

    UNITY_LOOP
    for (int i = -step; i <= step; ++i)
    {
        color += SAMPLE_TEXTURE2D(tex, samp, origin + stepLen * i).rgb;
    }

    return color * rcp(step * 2.0 + 1.0);
}

half4 MotionBlurPass(float2 currUV)
{
    half3 sourceColor = SAMPLE_TEXTURE2D_LOD(_BlendedFrameTexture, sampler_BlendedFrameTexture, currUV, 0).rgb;

    #if _ENABLE_MOTION_BLUR
        float2 motionVector = SAMPLE_TEXTURE2D_LOD(_MotionVectorTexture, sampler_MotionVectorTexture, currUV, 0).xy;
        
        float vecPixelLen = length(motionVector * _BlendedFrameTexture_TexelSize.zw);
        float trust = GetTrust(vecPixelLen, _MotionBlurRangeMin, _MotionBlurRangeMax);
        half3 blurColor = SampleMotionBlur(_BlendedFrameTexture, sampler_BlendedFrameTexture,
            currUV, motionVector * _MotionBlurIntensity, _MotionBlurSampleStep);
        
        sourceColor = lerp(sourceColor, blurColor, trust);
    #endif

    return float4(sourceColor, 1.0);
}

#endif
