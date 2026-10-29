// SimpleSonarShader scripts and shaders were written by Drew Okenfuss.
// Updated for native Universal Render Pipeline (URP) and Built-in dual compatibility.
Shader "MadeByProfessorOakie/SimpleSonarShader" {
	Properties {
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Albedo (RGB)", 2D) = "white" {}
		_Glossiness("Smoothness", Range(0,1)) = 0.5
		_Metallic("Metallic", Range(0,1)) = 0.0
		_RingColor("Ring Color", Color) = (0.2, 0.85, 1.0, 1)
		_RingColorIntensity("Ring Color Intensity", float) = 2.5
		_RingSpeed("Ring Speed", float) = 15.0
		_RingWidth("Ring Width", float) = 2.0
		_RingIntensityScale("Ring Range", float) = 1.0
		_RingTex("Ring Texture", 2D) = "white" {}
	}
	SubShader {
		Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
		LOD 200

		Pass {
			Name "ForwardLit"
			Tags { "LightMode" = "UniversalForward" }

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			struct Attributes {
				float4 positionOS   : POSITION;
				float3 normalOS     : NORMAL;
				float2 uv           : TEXCOORD0;
			};

			struct Varyings {
				float4 positionCS   : SV_POSITION;
				float3 positionWS   : TEXCOORD0;
				float3 normalWS     : TEXCOORD1;
				float2 uv           : TEXCOORD2;
			};

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);
			TEXTURE2D(_RingTex);
			SAMPLER(sampler_RingTex);

			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				half4 _Color;
				half4 _RingColor;
				half _Glossiness;
				half _Metallic;
				half _RingColorIntensity;
				half _RingSpeed;
				half _RingWidth;
				half _RingIntensityScale;
			CBUFFER_END

			// Sonar arrays passed by SimpleSonarShader scripts
			float4 _hitPts[20];
			float _Intensity[20];

			Varyings vert(Attributes input) {
				Varyings output;
				VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
				VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
				output.positionCS = vertexInput.positionCS;
				output.positionWS = vertexInput.positionWS;
				output.normalWS = normalInput.normalWS;
				output.uv = TRANSFORM_TEX(input.uv, _MainTex);
				return output;
			}

			half4 frag(Varyings input) : SV_Target {
				half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
				half3 baseCol = albedo.rgb;
				half3 finalCol = baseCol;
				half3 ringCol = _RingColor.rgb;

				// Sonar rings calculation
				for (int i = 0; i < 20; i++) {
					if (_Intensity[i] <= -4000.0) continue;
					float d = distance(_hitPts[i].xyz, input.positionWS);
					float maxRange = _Intensity[i] * _RingIntensityScale;
					float val = saturate(1.0 - (d / max(maxRange, 0.001)));
					float elapsed = max(0.0, _Time.y - _hitPts[i].w);
					float front = elapsed * _RingSpeed;
					float back = front - _RingWidth;

					if (d < front && d > back && val > 0.0) {
						float posInRing = (d - back) / max(_RingWidth, 0.001);
						float3 dir = input.positionWS - _hitPts[i].xyz;
						float angle = acos(clamp(dot(normalize(dir), float3(1.0, 0.0, 0.0)), -1.0, 1.0)) / 3.14159;
						half ringPattern = SAMPLE_TEXTURE2D(_RingTex, sampler_RingTex, float2(1.0 - posInRing, angle)).r;
						half strength = val * ringPattern * _RingColorIntensity;
						finalCol = lerp(finalCol, ringCol * _RingColorIntensity, saturate(strength));
					}
				}

				Light mainLight = GetMainLight();
				half NdotL = saturate(dot(normalize(input.normalWS), mainLight.direction));
				half3 lightCol = mainLight.color * (NdotL + 0.25);
				return half4(finalCol * lightCol, albedo.a);
			}
			ENDHLSL
		}
	}

	SubShader {
		Tags { "RenderType" = "Opaque" }
		LOD 200

		CGPROGRAM
		#pragma surface surf Standard fullforwardshadows
		#pragma target 3.0

		sampler2D _MainTex;
		sampler2D _RingTex;

		struct Input {
			float2 uv_MainTex;
			float3 worldPos;
		};

		half4 _hitPts[20];
		half _StartTime;
		half _Intensity[20];

		half _Glossiness;
		half _Metallic;
		fixed4 _Color;
		fixed4 _RingColor;
		half _RingColorIntensity;
		half _RingSpeed;
		half _RingWidth;
		half _RingIntensityScale;

		void surf(Input IN, inout SurfaceOutputStandard o) {
			fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
			o.Albedo = c.rgb;

			half DiffFromRingCol = abs(o.Albedo.r - _RingColor.r) + abs(o.Albedo.b - _RingColor.b) + abs(o.Albedo.g - _RingColor.g);

			for (int i = 0; i < 20; i++) {
				half d = distance(_hitPts[i], IN.worldPos);
				half intensity = _Intensity[i] * _RingIntensityScale;
				half val = (1 - (d / intensity));

				if (d < (_Time.y - _hitPts[i].w) * _RingSpeed && d >(_Time.y - _hitPts[i].w) * _RingSpeed - _RingWidth && val > 0) {
					half posInRing = (d - ((_Time.y - _hitPts[i].w) * _RingSpeed - _RingWidth)) / _RingWidth;

					float angle = acos(dot(normalize(IN.worldPos - _hitPts[i]), float3(1,0,0)));
					val *= tex2D(_RingTex, half2(1 - posInRing, angle));
					half3 tmp = _RingColor * val + c * (1 - val);

					half tempDiffFromRingCol = abs(tmp.r - _RingColor.r) + abs(tmp.b - _RingColor.b) + abs(tmp.g - _RingColor.g);
					if (tempDiffFromRingCol < DiffFromRingCol) {
						DiffFromRingCol = tempDiffFromRingCol;
						o.Albedo.r = tmp.r;
						o.Albedo.g = tmp.g;
						o.Albedo.b = tmp.b;
						o.Albedo.rgb *= _RingColorIntensity;
					}
				}
			}

			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;
		}
		ENDCG
	}
	FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
