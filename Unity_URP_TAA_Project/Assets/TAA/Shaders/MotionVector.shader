Shader "Hidden/Custom/TAA/MotionVector"
{
	SubShader
	{
		Pass  // 0
		{
			Name "Dynamic Objects Motion Vector Pass"
			
			Tags { "LightMode"="MotionVector" }
			
			Cull back
			ZWrite Off
			ZTest LEqual
			
			Stencil
			{
				Ref 1
				WriteMask 1
				Comp Always
				Pass Replace
				Fail Keep
				ZFail Keep
			}
			
			HLSLPROGRAM

			#pragma target 4.5

			#pragma vertex DynamicMotionVectorVertex
			#pragma fragment DynamicMotionVectorFragment

			#include "MotionVectorPass.hlsl"

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 prevPositionOS : TEXCOORD4;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct DynamicVaryings
			{
				float4 positionCS : SV_POSITION;
				float4 currPositionNDC : TEXCOORD0;
				float4 prevPositionNDC : TEXCOORD1;
				UNITY_VERTEX_OUTPUT_STEREO
			};
			
			DynamicVaryings DynamicMotionVectorVertex(Attributes input)
			{
				DynamicVaryings output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
				
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				float4 currPositionCS = mul(_CurrMatrixVP, mul(unity_ObjectToWorld, float4(input.positionOS.xyz, 1.0)));
				float4 prevPositionCS = mul(_PrevMatrixVP, mul(unity_MatrixPreviousM,
					float4(step(0.5, unity_MotionVectorsParams.x) ? input.prevPositionOS : input.positionOS.xyz, 1.0)));

				#if UNITY_REVERSED_Z
                    output.positionCS.z -= unity_MotionVectorsParams.z * output.positionCS.w;
                #else
                    output.positionCS.z += unity_MotionVectorsParams.z * output.positionCS.w;
                #endif
			
				output.currPositionNDC = GetVertexPositionNDC(currPositionCS);
				output.prevPositionNDC = GetVertexPositionNDC(prevPositionCS);
			
				return output;
			}
			
			float4 DynamicMotionVectorFragment(DynamicVaryings input) : SV_Target
			{
				float2 currUV = input.currPositionNDC.xy / input.currPositionNDC.w;
				float2 prevUV = input.prevPositionNDC.xy / input.prevPositionNDC.w;
				
				return float4(currUV - prevUV, 0.0, 0.0);
			}

			ENDHLSL
		}
		Pass  // 1
		{
			Name "Static Objects Motion Vector Pass"
			
			Cull Off
			ZWrite Off
			ZTest Always
			
			Stencil
			{
				Ref 1
				Comp NotEqual
				Pass Keep
				Fail Keep
			}
			
			HLSLPROGRAM

			#pragma target 4.5

			#pragma vertex StaticMotionVectorVertex
			#pragma fragment StaticMotionVectorFragment

			#include "MotionVectorPass.hlsl"

			struct Attributes
			{
				float4 positionOS : POSITION;
				float2 baseUV : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct StaticVaryings
			{
				float4 positionCS : SV_POSITION;
				float2 currUV : TEXCOORD0;
				float3 viewRay : TEXCOORD1;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			StaticVaryings StaticMotionVectorVertex(Attributes input)
			{
				StaticVaryings output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
			
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				output.currUV = input.baseUV - _JitterUV.xy * rcp(_ScaledScreenParams.xy);
			
				float depth = 1.0;
				#if UNITY_REVERSED_Z
					depth = 0.0;
				#endif
			
				// In order to recreate world position from depth map
				float3 positionWS = ComputeWorldSpacePosition(output.currUV, depth, _CurrMatrixInvVP);
				output.viewRay = positionWS - _WorldSpaceCameraPos.xyz;
			
				return output;
			}

			float4 StaticMotionVectorFragment(StaticVaryings input) : SV_Target
			{
				float posZ = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, input.currUV);
				float depth = Linear01Depth(posZ, _ZBufferParams);
				float3 positionWS = GetCameraPositionWS() + depth * input.viewRay;
			
				float2 prevUV = ComputeNormalizedDeviceCoordinates(positionWS, _PrevMatrixVP);
				
				return float4(input.currUV - prevUV, 0.0, 0.0);
			}

			ENDHLSL
		}
	}
}