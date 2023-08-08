Shader "Hidden/Custom/TAA/DebugBlit"
{
	Properties
	{
		_ShowIntensity("", Float) = 1.0
	}
	SubShader
	{
		Cull Off
		ZWrite Off
		ZTest Always

		Pass
		{
			Name "Debug Pass"
			
			HLSLPROGRAM

			#pragma target 4.5

			#pragma vertex DebugBlitVertex
			#pragma fragment DebugBlitFragment

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

			CBUFFER_START(UnityPerMaterial)
			float _ShowIntensity;
			CBUFFER_END

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

			Varyings DebugBlitVertex(Attributes input)
			{
			    Varyings output;
			    UNITY_SETUP_INSTANCE_ID(input);
			    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

			    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
			    output.currUV = input.baseUV;

			    return output;
			}

			half4 DebugBlitFragment(Varyings input) : SV_Target
			{
			    half4 src = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.currUV);
				src.rgb *= _ShowIntensity * _ShowIntensity;

				return src;
			}

			ENDHLSL
		}
	}
}
