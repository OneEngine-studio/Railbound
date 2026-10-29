Shader "Project/URP_WorldScanFX"
{
    Properties
    {
        [HDR] _WaveColor ("Wave Color (HDR)", Color) = (2.8, 0.7, 0.45, 1.0)
        [HDR] _CoreColor ("Core Leading Edge (HDR)", Color) = (3.5, 3.0, 2.5, 1.0)
        [HDR] _TrailColor ("Trail / Fill Color", Color) = (1.6, 0.4, 0.25, 0.4)
        _ScanCenter ("Scan Center (WS)", Vector) = (0, 0, 0, 0)
        _CurrentRadius ("Current Radius", Float) = 0.0
        _MaxRadius ("Max Radius", Float) = 130.0
        _WaveWidth ("Wave Width", Float) = 3.2
        _TrailLength ("Trail Length", Float) = 14.0
        _EdgeDetectSensitivity ("Edge Sensitivity", Float) = 2.5
        _GridFrequency ("Surface Ripple Frequency", Float) = 1.2
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+500"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 200

        Cull Front
        ZWrite Off
        ZTest Always
        Blend SrcAlpha One

        Pass
        {
            Name "WorldScanForwardPass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _WaveColor;
                float4 _CoreColor;
                float4 _TrailColor;
                float4 _ScanCenter;
                float _CurrentRadius;
                float _MaxRadius;
                float _WaveWidth;
                float _TrailLength;
                float _EdgeDetectSensitivity;
                float _GridFrequency;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.screenPos = ComputeScreenPos(vertexInput.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;

                // Sample Scene Depth
                float rawDepth = SampleSceneDepth(screenUV);
                #if UNITY_REVERSED_Z
                if (rawDepth <= 0.00002) discard; // Skybox / far clip
                #else
                if (rawDepth >= 0.99998) discard;
                #endif

                // Reconstruct World Space Position
                float3 worldPos = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);

                float dist = distance(worldPos, _ScanCenter.xyz);

                // Outside current wave expansion -> discard
                if (dist > _CurrentRadius + 0.1) discard;

                // Distance from active wavefront
                float waveDist = abs(dist - _CurrentRadius);

                // Leading edge wave profile (0 at waveWidth, 1 at crest)
                float leading = saturate(1.0 - (waveDist / max(_WaveWidth, 0.001)));
                float coreLeading = pow(leading, 3.5);

                // Trailing envelope behind the wave (soft fill on floor & objects)
                float trailingDist = _CurrentRadius - dist;
                float trailing = saturate(1.0 - (trailingDist / max(_TrailLength, 0.001)));
                trailing = pow(trailing, 1.8);

                // Depth-edge detection: samples 4 neighbor depth taps to detect silhouette edges
                float2 texel = _ScreenParams.zw - 1.0;
                float depthRight = SampleSceneDepth(screenUV + float2(texel.x * 2.0, 0.0));
                float depthLeft  = SampleSceneDepth(screenUV - float2(texel.x * 2.0, 0.0));
                float depthUp    = SampleSceneDepth(screenUV + float2(0.0, texel.y * 2.0));
                float depthDown  = SampleSceneDepth(screenUV - float2(0.0, texel.y * 2.0));

                float depthDiff = abs(rawDepth - depthRight) + abs(rawDepth - depthLeft) +
                                  abs(rawDepth - depthUp)    + abs(rawDepth - depthDown);

                float edgeFactor = saturate(depthDiff * _EdgeDetectSensitivity * 1000.0);

                // Surface concentric ripples
                float ripple = sin((dist - _CurrentRadius) * _GridFrequency) * 0.5 + 0.5;
                ripple = pow(ripple, 2.0);

                // Overall distance fade as the scan expands towards max radius
                float rangeFade = saturate(1.0 - (_CurrentRadius / max(_MaxRadius, 0.001)));
                rangeFade = pow(rangeFade, 0.45);

                // Combine components:
                // 1. Highlighted geometry edges on barrels, crates, stairs, obstacles
                // 2. Main wave crest with white-hot core
                // 3. Trailing surface wash with ripples
                half3 col = half3(0, 0, 0);

                // Edge outline glow on intersected geometry
                col += _WaveColor.rgb * edgeFactor * leading * 3.5;

                // Main wave crest
                col += _WaveColor.rgb * leading * 2.2;
                col += _CoreColor.rgb * coreLeading * 4.0;

                // Trailing surface wash with ripples
                col += _TrailColor.rgb * trailing * (0.35 + ripple * 0.4);

                // Final alpha and range modulation
                float alpha = saturate((leading * 1.5 + edgeFactor * leading * 2.0 + trailing * 0.5) * rangeFade);
                if (alpha < 0.005) discard;

                col *= rangeFade;

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}

