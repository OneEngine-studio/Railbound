Shader "MadeByProfessorOakie/URP_SonarWaveRing" {
    Properties {
        _Color ("Ring Color", Color) = (0.0, 0.9, 1.0, 1.0)
        _MainTex ("Ring Texture", 2D) = "white" {}
        _Emission ("Emission Intensity", Float) = 3.0
    }
    SubShader {
        Tags {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass {
            Name "SonarWavePass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Emission;
            CBUFFER_END

            Varyings vert(Attributes input) {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half alpha = tex.r * input.color.a * _Color.a;
                half3 rgb = _Color.rgb * tex.rgb * _Emission * input.color.rgb;
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}

