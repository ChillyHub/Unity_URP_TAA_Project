Shader "Hidden/Custom/TAA/MotionBlur"
{
	SubShader
	{
		Cull Off
		ZWrite Off
		ZTest Always

		Pass
		{
			Name "Motion Blur Pass"
			
			HLSLPROGRAM

			#pragma target 4.5

			#pragma shader_feature _ENABLE_YCOCG
			#pragma shader_feature _ENABLE_MOTION_BLUR

			#pragma vertex MotionBlurVertex
			#pragma fragment MotionBlurFragment

			#include "MotionBlurPass.hlsl"

			struct Attributes
			{
			    float4 positionOS : POSITION;
			    float2 baseUV : TEXCOORD0;
			    UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct Varyings
			{
			    float4 positionCS : SV_POSITION;
			    float2 currUV : TEXCOORD0;
			    UNITY_VERTEX_OUTPUT_STEREO
			};

			Varyings MotionBlurVertex(Attributes input)
			{
			    Varyings output;
			    UNITY_SETUP_INSTANCE_ID(input);
			    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

			    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
			    output.currUV = input.baseUV;

			    return output;
			}

			half4 MotionBlurFragment(Varyings input) : SV_Target
			{
			    return MotionBlurPass(input.currUV);
			}

			ENDHLSL
		}
	}
}