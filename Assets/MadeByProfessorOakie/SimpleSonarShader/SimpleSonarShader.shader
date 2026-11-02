// SimpleSonarShader scripts and shaders were written by Drew Okenfuss.
// Updated for native High Definition Render Pipeline (HDRP).
Shader "MadeByProfessorOakie/SimpleSonarShader"
{
    Properties
    {
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
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "HDRenderPipeline" "Queue" = "Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
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
                float4 _Color;
                float4 _RingColor;
                float _Glossiness;
                float _Metallic;
                float _RingColorIntensity;
                float _RingSpeed;
                float _RingWidth;
                float _RingIntensityScale;
            CBUFFER_END

            float4 _hitPts[20];
            float _Intensity[20];

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionRWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionRWS);
                output.positionWS = GetAbsolutePositionWS(positionRWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                float3 finalCol = albedo.rgb;
                float3 ringCol = _RingColor.rgb;

                for (int i = 0; i < 20; i++)
                {
                    if (_Intensity[i] <= -4000.0) continue;
                    float d = distance(_hitPts[i].xyz, input.positionWS);
                    float maxRange = _Intensity[i] * _RingIntensityScale;
                    float val = saturate(1.0 - (d / max(maxRange, 0.001)));
                    float elapsed = max(0.0, _Time.y - _hitPts[i].w);
                    float front = elapsed * _RingSpeed;
                    float back = front - _RingWidth;

                    if (d < front && d > back && val > 0.0)
                    {
                        float posInRing = (d - back) / max(_RingWidth, 0.001);
                        float3 dir = input.positionWS - _hitPts[i].xyz;
                        float angle = acos(clamp(dot(normalize(dir), float3(1.0, 0.0, 0.0)), -1.0, 1.0)) / 3.14159;
                        float ringPattern = SAMPLE_TEXTURE2D(_RingTex, sampler_RingTex, float2(1.0 - posInRing, angle)).r;
                        float strength = val * ringPattern * _RingColorIntensity;
                        finalCol = lerp(finalCol, ringCol * _RingColorIntensity, saturate(strength));
                    }
                }

                return float4(finalCol * GetCurrentExposureMultiplier(), albedo.a);
            }
            ENDHLSL
        }
    }
}
