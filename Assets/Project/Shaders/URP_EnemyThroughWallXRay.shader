Shader "Railbound/URP_EnemyThroughWallXRay"
{
    Properties
    {
        [Header(Holographic XRay Color)]
        _Color ("Silhouette Color", Color) = (1.0, 0.12, 0.22, 1.0)
        _RimColor ("Rim Outline Color", Color) = (1.0, 0.35, 0.45, 1.0)
        _EmissionIntensity ("Emission Intensity", Float) = 3.5

        [Header(Silhouette Tuning)]
        _FresnelPower ("Fresnel Power", Range(0.5, 6.0)) = 2.0
        _InnerAlpha ("Inner Body Fill Alpha", Range(0.0, 1.0)) = 0.35
        _RimAlpha ("Rim Outline Alpha", Range(0.0, 1.0)) = 1.0

        [Header(Cybernetic Scanlines)]
        _ScanlineDensity ("Scanline Density", Float) = 32.0
        _ScanlineSpeed ("Scanline Scroll Speed", Float) = 6.0
        _ScanlineStrength ("Scanline Strength", Range(0.0, 1.0)) = 0.4

        [Header(Pulse Animation)]
        _PulseSpeed ("Pulse Speed", Float) = 5.0
        _PulseAmplitude ("Pulse Amplitude", Range(0.0, 0.5)) = 0.2

        [Header(Depth and Occlusion)]
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest Mode", Float) = 8 // 8 = Always (shows through all walls)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+90"
            "RenderPipeline" = "HDRenderPipeline"
            "DisableBatching" = "True"
        }

        LOD 200
        Blend SrcAlpha One
        ZWrite Off
        ZTest [_ZTest]
        Cull Back

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            HLSLPROGRAM
            #pragma target 4.5
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionRWS  : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float2 uv           : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _RimColor;
                float _EmissionIntensity;
                float _FresnelPower;
                float _InnerAlpha;
                float _RimAlpha;
                float _ScanlineDensity;
                float _ScanlineSpeed;
                float _ScanlineStrength;
                float _PulseSpeed;
                float _PulseAmplitude;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionRWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionRWS);
                output.positionRWS = positionRWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionRWS);
                float3 absWorldPos = GetAbsolutePositionWS(input.positionRWS);

                // 1. Fresnel Rim Outline
                float NdotV = saturate(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - NdotV, _FresnelPower);

                // 2. Holographic Horizontal Scanlines
                float scanline = sin(absWorldPos.y * _ScanlineDensity - _Time.y * _ScanlineSpeed) * 0.5 + 0.5;
                float scanMod = 1.0 - (_ScanlineStrength * (1.0 - scanline));

                // 3. Heartbeat / Sonar Pulse
                float pulse = 1.0 + _PulseAmplitude * sin(_Time.y * _PulseSpeed);

                // 4. Color & Alpha Blending
                float3 baseCol = _Color.rgb;
                float3 rimCol = _RimColor.rgb;
                float3 finalColor = lerp(baseCol, rimCol, fresnel) * (_EmissionIntensity * pulse * scanMod);

                float alpha = lerp(_InnerAlpha, _RimAlpha, fresnel) * _Color.a * scanMod;

                return float4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
}
