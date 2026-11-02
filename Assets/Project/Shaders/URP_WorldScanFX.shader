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
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch
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

                // Sample HDRP Scene Depth Pyramid
                float rawDepth = LoadCameraDepth(pixelCoord);
                if (IsSky(rawDepth)) discard;

                // Reconstruct Camera-Relative World Position, then convert to Absolute World Position
                PositionInputs posInput = GetPositionInput(input.positionCS.xy, _ScreenSize.zw, rawDepth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
                float3 worldPos = GetAbsolutePositionWS(posInput.positionWS);

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
                ripple = pow(ripple, 2.0);

                // Overall distance fade as the scan expands towards max radius
                float rangeFade = saturate(1.0 - (_CurrentRadius / max(_MaxRadius, 0.001)));
                rangeFade = pow(rangeFade, 0.45);

                float3 col = float3(0, 0, 0);

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

                // Pre-expose for HDRP camera buffer
                col *= rangeFade * GetCurrentExposureMultiplier();

                return float4(col, alpha);
            }
            ENDHLSL
        }
    }
}
