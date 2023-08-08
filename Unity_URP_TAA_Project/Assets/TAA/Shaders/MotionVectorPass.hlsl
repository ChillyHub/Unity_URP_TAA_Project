#ifndef CUSTOM_MOTION_VECTOR_PASS_INCLUDED
#define CUSTOM_MOTION_VECTOR_PASS_INCLUDED

#include "TemporalAAInput.hlsl"

float4 GetVertexPositionNDC(float4 positionCS)
{
	float4 positionNDC;
	float4 ndc = positionCS * 0.5f;
	positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
	positionNDC.zw = positionCS.zw;

	return positionNDC;
}

float4 StaticMotionVectorCSPass(float2 currUV)
{
	float posZ = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, currUV, 0);
	float3 positionWS = ComputeWorldSpacePosition(currUV, posZ, _CurrMatrixInvVP);

	float2 prevUV = ComputeNormalizedDeviceCoordinates(positionWS, _PrevMatrixVP);

	return float4(currUV - prevUV, 0.0, 0.0);
}

#endif
