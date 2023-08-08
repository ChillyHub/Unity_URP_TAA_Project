#ifndef CUSTOM_TEMPORAL_AA_INPUT_INCLUDED
#define CUSTOM_TEMPORAL_AA_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

#include "SampleUtils.hlsl"

TEXTURE2D(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);

TEXTURE2D(_MotionVectorTexture);
SAMPLER(sampler_MotionVectorTexture);
TEXTURE2D(_HistoryFrameTexture);
SAMPLER(sampler_HistoryFrameTexture);
TEXTURE2D(_CurrentFrameTexture);
SAMPLER(sampler_CurrentFrameTexture);

TEXTURE2D(_BlendedFrameTexture);
SAMPLER(sampler_BlendedFrameTexture);


CBUFFER_START(UnityPerMaterial)
float4x4 _PrevMatrixVP;
float4x4 _PrevMatrixInvVP;
float4x4 _CurrMatrixVP;
float4x4 _CurrMatrixInvVP;
float4 _JitterUV;

float4 _CurrentFrameTexture_TexelSize;
float4 _CameraDepthTexture_TexelSize;
float _FixedBlendWeight;
float _RangeWeightMin;
float _RangeWeightMax;

float4 _BlendedFrameTexture_TexelSize;
float _MotionBlurIntensity;
float _MotionBlurRangeMin;
float _MotionBlurRangeMax;
float _MotionBlurSampleStep;
CBUFFER_END

#endif
