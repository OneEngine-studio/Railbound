Shader "Project/URP_WorldScanFX"
{
    Properties
    {
        [HDR] _WaveColor ("Wave Color (HDR)", Color) = (0.0, 1.5, 2.4, 1.0)
        [HDR] _CoreColor ("Core Leading Edge (HDR)", Color) = (0.2, 1.8, 2.6, 1.0)
        [HDR] _TrailColor ("Trail / Fill Color", Color) = (0.0, 0.45, 1.0, 0.3)
        _ScanCenter ("Scan Center (WS)", Vector) = (0, 0, 0, 0)
        _CurrentRadius ("Current Radius", Float) = 0.0
        _MaxRadius ("Max Radius", Float) = 140.0
        _WaveWidth ("Wave Width", Float) = 3.5
        _TrailLength ("Trail Length", Float) = 22.0
        _EdgeDetectSensitivity ("Edge Sensitivity", Float) = 2.8
        _GridFrequency ("Surface Ripple Frequency", Float) = 1.6
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+80"
            "RenderPipeline" = "HDRenderPipeline"
        }
        LOD 200

        Cull Front
        ZWrite Off
        ZTest Always
        Blend SrcAlpha One

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
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
                float3 positionRWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionRWS);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                uint2 pixelCoord = uint2(input.positionCS.xy);

                // Sample HDRP Scene Depth
                float rawDepth = LoadCameraDepth(pixelCoord);
                if (IsSky(rawDepth)) discard;

                // Reconstruct Absolute World Position from Depth
                PositionInputs posInput = GetPositionInput(input.positionCS.xy, _ScreenSize.zw, rawDepth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
                float3 worldPos = GetAbsolutePositionWS(posInput.positionWS);

                float dist = distance(worldPos, _ScanCenter.xyz);

                // Discard pixels outside current wave crest or behind trail
                if (dist > _CurrentRadius + 0.2) discard;
                float trailingDist = _CurrentRadius - dist;
                if (trailingDist > _TrailLength) discard;

                // Distance from active wavefront crest
                float waveDist = abs(dist - _CurrentRadius);

                // Leading edge wave profile (0 at waveWidth, 1 at crest)
                float leading = saturate(1.0 - (waveDist / max(_WaveWidth, 0.001)));
                float coreLeading = pow(leading, 3.5);

                // Trailing envelope behind the wavefront
                float trailing = saturate(1.0 - (trailingDist / max(_TrailLength, 0.001)));
                trailing = pow(trailing, 1.8);

                // Depth-edge detection: 4-tap depth difference to outline 3D geometry silhouettes
                uint2 maxPixel = uint2(max(float2(1.0, 1.0), _ScreenSize.xy - 1.0));
                float depthRight = LoadCameraDepth(min(pixelCoord + uint2(2, 0), maxPixel));
                float depthLeft  = LoadCameraDepth(uint2(max(int2(0, 0), int2(pixelCoord) - int2(2, 0))));
                float depthUp    = LoadCameraDepth(min(pixelCoord + uint2(0, 2), maxPixel));
                float depthDown  = LoadCameraDepth(uint2(max(int2(0, 0), int2(pixelCoord) - int2(0, 2))));

                float depthDiff = abs(rawDepth - depthRight) + abs(rawDepth - depthLeft) +
                                  abs(rawDepth - depthUp)    + abs(rawDepth - depthDown);

                float edgeFactor = saturate(depthDiff * _EdgeDetectSensitivity * 1000.0);

                // Surface concentric ripples
                float ripple = sin((dist - _CurrentRadius) * _GridFrequency) * 0.5 + 0.5;
                ripple = pow(ripple, 2.5);

                // Distance fade as scan expands towards max radius
                float rangeFade = saturate(1.0 - (_CurrentRadius / max(_MaxRadius, 0.001)));
                rangeFade = pow(rangeFade, 0.4);

                float3 col = float3(0, 0, 0);

                // 1. Sharp glowing silhouette edge highlights on 3D objects
                col += _WaveColor.rgb * (edgeFactor * leading * 2.2 + edgeFactor * trailing * 0.7);

                // 2. Focused vibrant wave crest
                col += _WaveColor.rgb * (leading * 1.2);
                col += _CoreColor.rgb * (coreLeading * 1.0);

                // 3. Trailing holographic scanline / ripple wash
                col += _TrailColor.rgb * (trailing * (0.15 + ripple * 0.4));

                // Final alpha blend weight
                float alpha = saturate((leading * 1.2 + edgeFactor * leading * 1.3 + trailing * 0.5) * rangeFade);
                if (alpha < 0.008) discard;

                col *= rangeFade;

                return float4(col, alpha);
            }
            ENDHLSL
        }
    }
}
