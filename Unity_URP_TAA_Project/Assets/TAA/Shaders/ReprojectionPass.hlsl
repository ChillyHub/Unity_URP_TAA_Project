#ifndef CUSTOM_REPROJECTION_PASS_INCLUDED
#define CUSTOM_REPROJECTION_PASS_INCLUDED

#include "TemporalAAInput.hlsl"

void Sample3x3(out half3 colors[9], TEXTURE2D(tex), SAMPLER(samp), float2 uv, float2 duv, bool isDepth = false)
{
    float du = duv.x;
    float dv = duv.y;

    const float2 deltaUV[9] = {
        { -du, -dv }, { 0.0, -dv }, { du, -dv },
        { -du, 0.0 }, { 0.0, 0.0 }, { du, 0.0 },
        { -du,  dv }, { 0.0,  dv }, { du,  dv }
    };
                                
    UNITY_UNROLL
    for (int i = 0; i < 9; ++i)
    {
        half4 map = SAMPLE_TEXTURE2D_LOD(tex, samp, uv + deltaUV[i], 0);
        colors[i] = lerp(MappingColor(map.rgb), map.rgb, step(0.5, isDepth));
    }
}

void Sample3x3(out half3 colors[9], out half3 midColor, TEXTURE2D(tex), SAMPLER(samp), float2 uv, float2 duv)
{
    Sample3x3(colors, tex, samp, uv, duv);
    midColor = colors[4];
}

float2 SampleClosestPixelUV3x3(TEXTURE2D(tex), SAMPLER(samp), float2 uv, float2 duv)
{
    half3 depths[9];
    Sample3x3(depths, tex, samp, uv, duv, true);

    float du = duv.x;
    float dv = duv.y;

    float minDepth = FLT_MAX;
    float2 minUV = uv;
    const float2 deltaUV[9] = {
        { -du, -dv }, { 0.0, -dv }, { du, -dv },
        { -du, 0.0 }, { 0.0, 0.0 }, { du, 0.0 },
        { -du,  dv }, { 0.0,  dv }, { du,  dv }
    };

    UNITY_UNROLL
    for (int i = 0; i < 9; ++i)
    {
        #if UNITY_REVERSED_Z
            const float l = step(minDepth, depths[i].r);
        #else
            const float l = step(depths[i].r, minDepth);
        #endif
    
        minUV = lerp(minUV, minUV + deltaUV[i], l);
        minDepth = lerp(minDepth, depths[i].r, l);
    }

    return minUV;
}

half3 SampleMinMax3x3(out half3 minVar, out half3 maxVar, TEXTURE2D(tex), SAMPLER(samp), float2 uv, float2 duv)
{
    half3 midColor;
    half3 colros[9];
    Sample3x3(colros, midColor, tex, samp, uv, duv);

    minVar = maxVar = colros[0];

    UNITY_UNROLL
    for (int i = 1; i < 9; ++i)
    {
        minVar = min(minVar, colros[i]);
        maxVar = max(maxVar, colros[i]);
    }

    #if _ENABLE_YCOCG
        // float2 chromaExtent = 0.25 * 0.5 * (maxVar.r - minVar.r);
        // float2 chromaCenter = midColor.gb;
        // minVar.gb = chromaCenter - chromaExtent;
        // maxVar.gb = chromaCenter + chromaExtent;
    #endif

    return midColor;
}

void SampleMinMax3x3(out half3 minVar, out half3 maxVar, out half3 currColor,
    TEXTURE2D(tex), SAMPLER(samp), float2 uv, float2 duv)
{
    currColor = SampleMinMax3x3(minVar, maxVar, tex, samp, uv, duv);
}

half3 ClipBoxColor(half3 sourceColor, half3 minColor, half3 maxColor)
{
    half3 midColor = (minColor + maxColor) * 0.5;
    half3 toEdgeVec = (maxColor - minColor) * 0.5;
    
    half3 toSrcVec = sourceColor - midColor;
    half3 unitVec = abs(toSrcVec / max(toEdgeVec, HALF_EPS));
    float unit = max(unitVec.x, max(unitVec.y, max(unitVec.z, HALF_EPS)));
    half3 res = lerp(sourceColor, midColor + toSrcVec * rcp(unit), step(1.0, unit));

    return res;
}

float GetBlendWeight(half3 colorA, half3 colorB, float2 motionVector, float2 textureSize)
{
    #if _BLEND_FIXED
        return _FixedBlendWeight;
    #endif

    float weight = 0.0;
    
    #if _BLEND_MOTION || _BLEND_MOTION_LUMINANCE
        weight += saturate(length(motionVector) * textureSize);
    #endif
    
    #if _BLEND_LUMINANCE || _BLEND_MOTION_LUMINANCE
        #if _ENABLE_YCOCG
            half la = colorA.r;
            half lb = colorB.r;
        #else
            half la = Luminance(colorA);
            half lb = Luminance(colorB);
        #endif
        
        half t1 = abs(la - lb) / max(la, max(lb, 0.2));
        weight += t1 * t1;
    #endif
    
    #if _BLEND_MOTION_LUMINANCE
        weight *= 0.5;
    #endif

    return lerp(_RangeWeightMin, _RangeWeightMax, weight);
}

half4 ReprojectionPass(float2 currUV)
{
    #if _ENABLE_SAMPLE_CLOSEST_MOTION_VECTOR
        currUV = SampleClosestPixelUV3x3(_CameraDepthTexture, sampler_CameraDepthTexture,
            currUV, _CameraDepthTexture_TexelSize.xy);
    #endif
    
    float2 motionVector = SAMPLE_TEXTURE2D_LOD(_MotionVectorTexture, sampler_MotionVectorTexture, currUV, 0).xy;
    
    half4 historyFrame = SAMPLE_TEXTURE2D_LOD(_HistoryFrameTexture, sampler_HistoryFrameTexture, currUV - motionVector, 0);

    half3 historyColor = MappingColor(historyFrame.rgb);

    half3 minVar;
    half3 maxVar;
    half3 currentColor;
    SampleMinMax3x3(minVar, maxVar, currentColor, 
        _CurrentFrameTexture, sampler_CurrentFrameTexture, currUV, _CurrentFrameTexture_TexelSize.xy);
    
    historyColor = ClipBoxColor(historyColor, minVar, maxVar);

    half3 blendColor = lerp(historyColor, currentColor,
        GetBlendWeight(historyColor, currentColor, motionVector, _CurrentFrameTexture_TexelSize.zw));

    return half4(ResolveColor(blendColor), 1.0);
}

#endif
