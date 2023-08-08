Shader "Hidden/Custom/TAA/Reprojection"
{
	SubShader
	{
		Cull Off
		ZWrite Off
		ZTest Always

		Pass
		{
			Name "Reprojection Pass"
			
			HLSLPROGRAM

			#pragma target 4.5

			#pragma shader_feature _ENABLE_YCOCG
			#pragma shader_feature _ENABLE_SAMPLE_CLOSEST_MOTION_VECTOR
			#pragma shader_feature _ _BLEND_FIXED _BLEND_MOTION _BLEND_LUMINANCE _BLEND_MOTION_LUMINANCE

			#pragma vertex ReprojectionVertex
			#pragma fragment ReprojectionFragment

			#include "ReprojectionPass.hlsl"

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

			Varyings ReprojectionVertex(Attributes input)
			{
			    Varyings output;
			    UNITY_SETUP_INSTANCE_ID(input);
			    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

			    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
			    output.currUV = input.baseUV;

			    return output;
			}

			half4 ReprojectionFragment(Varyings input) : SV_Target
			{
			    return ReprojectionPass(input.currUV);
			}

			ENDHLSL
		}
	}
}