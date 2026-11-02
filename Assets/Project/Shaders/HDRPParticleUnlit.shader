Shader "HDRP/Particles/Unlit"
{
    Properties
    {
        [MainColor] _UnlitColor("Color", Color) = (1,1,1,1)
        [MainTexture] _UnlitColorMap("ColorMap", 2D) = "white" {}
        [ToggleUI] _UseVertexColor("Use Vertex Color", Float) = 1.0
        [ToggleUI] _LuminanceToAlpha("Luminance To Alpha", Float) = 0.0
        [HideInInspector] _AlphaRemapMin("AlphaRemapMin", Float) = 0.0
        [HideInInspector] _AlphaRemapMax("AlphaRemapMax", Float) = 1.0

        [HDR] _EmissiveColor("EmissiveColor", Color) = (0, 0, 0)
        _EmissiveColorMap("EmissiveColorMap", 2D) = "white" {}
        [HideInInspector] _EmissiveColorLDR("EmissiveColor LDR", Color) = (0, 0, 0)
        [ToggleUI] _AlbedoAffectEmissive("Albedo Affect Emissive", Float) = 0.0
        [HideInInspector] _EmissiveIntensityUnit("Emissive Mode", Int) = 0
        [ToggleUI] _UseEmissiveIntensity("Use Emissive Intensity", Int) = 0
        _EmissiveIntensity("Emissive Intensity", Float) = 1
        _EmissiveExposureWeight("Emissive Pre Exposure", Range(0.0, 1.0)) = 1.0

        _DistortionVectorMap("DistortionVectorMap", 2D) = "black" {}
        [ToggleUI] _DistortionEnable("Enable Distortion", Float) = 0.0
        [ToggleUI] _DistortionOnly("Distortion Only", Float) = 0.0
        [ToggleUI] _DistortionDepthTest("Distortion Depth Test Enable", Float) = 1.0
        [Enum(Add, 0, Multiply, 1, Replace, 2)] _DistortionBlendMode("Distortion Blend Mode", Int) = 0
        [HideInInspector] _DistortionSrcBlend("Distortion Blend Src", Int) = 0
        [HideInInspector] _DistortionDstBlend("Distortion Blend Dst", Int) = 0
        [HideInInspector] _DistortionBlurSrcBlend("Distortion Blur Blend Src", Int) = 0
        [HideInInspector] _DistortionBlurDstBlend("Distortion Blur Blend Dst", Int) = 0
        [HideInInspector] _DistortionBlurBlendMode("Distortion Blur Blend Mode", Int) = 0
        _DistortionScale("Distortion Scale", Float) = 1
        _DistortionVectorScale("Distortion Vector Scale", Float) = 2
        _DistortionVectorBias("Distortion Vector Bias", Float) = -1
        _DistortionBlurScale("Distortion Blur Scale", Float) = 1
        _DistortionBlurRemapMin("DistortionBlurRemapMin", Float) = 0.0
        _DistortionBlurRemapMax("DistortionBlurRemapMax", Float) = 1.0

        // Transparency (defaulted to Transparent Alpha Blend so decals/particles never render as opaque black boxes)
        [ToggleUI] _AlphaCutoffEnable("Alpha Cutoff Enable", Float) = 0.0
        _AlphaCutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _TransparentSortPriority("_TransparentSortPriority", Float) = 0

        _SurfaceType("__surfacetype", Float) = 1.0
        _BlendMode("__blendmode", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 10.0
        [HideInInspector] _AlphaSrcBlend("__alphaSrc", Float) = 1.0
        [HideInInspector] _AlphaDstBlend("__alphaDst", Float) = 10.0
        [HideInInspector][ToggleUI] _ZWrite("__zw", Float) = 0.0
        [HideInInspector][ToggleUI] _TransparentZWrite("_TransparentZWrite", Float) = 0.0
        [HideInInspector] _CullMode("__cullmode", Float) = 0.0
        [Enum(UnityEditor.Rendering.HighDefinition.TransparentCullMode)] _TransparentCullMode("_TransparentCullMode", Int) = 0
        [Enum(UnityEditor.Rendering.HighDefinition.OpaqueCullMode)] _OpaqueCullMode("_OpaqueCullMode", Int) = 0
        [HideInInspector] _ZTestModeDistortion("_ZTestModeDistortion", Int) = 8
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTestTransparent("Transparent ZTest", Int) = 4
        [HideInInspector] _ZTestDepthEqualForOpaque("_ZTestDepthEqualForOpaque", Int) = 4

        [ToggleUI] _EnableFogOnTransparent("Enable Fog", Float) = 1.0
        [ToggleUI] _DoubleSidedEnable("Double sided enable", Float) = 1.0
        [Enum(Auto, 0, On, 1, Off, 2)] _DoubleSidedGIMode("Double sided GI mode", Float) = 0

        [HideInInspector] _StencilRef("_StencilRef", Int) = 0
        [HideInInspector] _StencilWriteMask("_StencilWriteMask", Int) = 3
        [HideInInspector] _StencilRefDepth("_StencilRefDepth", Int) = 0
        [HideInInspector] _StencilWriteMaskDepth("_StencilWriteMaskDepth", Int) = 8
        [HideInInspector] _StencilRefMV("_StencilRefMV", Int) = 32
        [HideInInspector] _StencilWriteMaskMV("_StencilWriteMaskMV", Int) = 32

        [ToggleUI] _AddPrecomputedVelocity("AddPrecomputedVelocity", Float) = 0.0
        [HideInInspector] _StencilRefDistortionVec("_StencilRefDistortionVec", Int) = 2
        [HideInInspector] _StencilWriteMaskDistortionVec("_StencilWriteMaskDistortionVec", Int) = 2

        _EmissionColor("Color", Color) = (1, 1, 1)
        [HideInInspector] _IncludeIndirectLighting("_IncludeIndirectLighting", Float) = 1.0

        [HideInInspector] _MainTex("Albedo", 2D) = "white" {}
        [HideInInspector] _Color("Color", Color) = (1,1,1,1)
        [HideInInspector] _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [HideInInspector] _UnlitColorMap_MipInfo("_UnlitColorMap_MipInfo", Vector) = (0, 0, 0, 0)
    }

    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

    #pragma shader_feature_local _ALPHATEST_ON
    #pragma shader_feature_local_fragment _EMISSIVE_COLOR_MAP
    #pragma shader_feature _SURFACE_TYPE_TRANSPARENT
    #pragma shader_feature_local _ENABLE_FOG_ON_TRANSPARENT
    #pragma shader_feature_local _ADD_PRECOMPUTED_VELOCITY

    // Always treat HDRP/Particles/Unlit as transparent so ApplyBlendMode premultiplies opacity
    #ifndef _SURFACE_TYPE_TRANSPARENT
    #define _SURFACE_TYPE_TRANSPARENT
    #endif

    // Always pass vertex color (ParticleSystem / TrailRenderer color & alpha over lifetime)
    #define ATTRIBUTES_NEED_COLOR
    #define VARYINGS_NEED_COLOR

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/FragInputs.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPass.cs.hlsl"

    TEXTURE2D(_DistortionVectorMap);
    SAMPLER(sampler_DistortionVectorMap);

    TEXTURE2D(_UnlitColorMap);
    SAMPLER(sampler_UnlitColorMap);

    TEXTURE2D(_EmissiveColorMap);
    SAMPLER(sampler_EmissiveColorMap);

    CBUFFER_START(UnityPerMaterial)
    float4 _UnlitColor;
    float4 _UnlitColorMap_ST;
    float4 _UnlitColorMap_TexelSize;
    float4 _UnlitColorMap_MipInfo;
    float _UseVertexColor;
    float _LuminanceToAlpha;

    float3 _EmissiveColor;
    float4 _EmissiveColorMap_ST;
    float _EmissiveExposureWeight;

    float _AlphaCutoff;
    float _DistortionScale;
    float _DistortionVectorScale;
    float _DistortionVectorBias;
    float _DistortionBlurScale;
    float _DistortionBlurRemapMin;
    float _DistortionBlurRemapMax;
    float4 _DistortionVectorMap_ST;
    float _AlphaRemapMin;
    float _AlphaRemapMax;
    float _BlendMode;
    float3 _EmissionColor;
    float _IncludeIndirectLighting;
    CBUFFER_END

    int _ObjectId;
    int _PassValue;

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Name "SceneSelectionPass"
            Tags { "LightMode" = "SceneSelectionPass" }

            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch
            #pragma multi_compile_instancing

            #define SHADERPASS SHADERPASS_DEPTH_ONLY
            #define SCENESELECTIONPASS
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/PickingSpaceTransforms.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/Unlit.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/ShaderPass/UnlitDepthPass.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/UnlitData.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassDepthOnly.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma editor_sync_compilation
            ENDHLSL
        }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            Blend [_SrcBlend] [_DstBlend], [_AlphaSrcBlend] [_AlphaDstBlend]
            Blend 1 SrcAlpha OneMinusSrcAlpha
            ZWrite [_ZWrite]
            ZTest [_ZTestDepthEqualForOpaque]
            Cull [_CullMode]

            HLSLPROGRAM
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY

            #ifdef DEBUG_DISPLAY
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Debug/DebugDisplay.hlsl"
            #endif

            #define SHADERPASS SHADERPASS_FORWARD_UNLIT

            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/Unlit.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/ShaderPass/UnlitSharePass.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Sampling/SampleUVMapping.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/BuiltinUtilities.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/MaterialUtilities.hlsl"

            void GetSurfaceAndBuiltinData(FragInputs input, float3 V, inout PositionInputs posInput, out SurfaceData surfaceData, out BuiltinData builtinData)
            {
                float2 unlitColorMapUv = TRANSFORM_TEX(input.texCoord0.xy, _UnlitColorMap);
                float4 texSample = SAMPLE_TEXTURE2D(_UnlitColorMap, sampler_UnlitColorMap, unlitColorMapUv);

                float4 vColor = (_UseVertexColor > 0.5) ? input.color : float4(1.0, 1.0, 1.0, 1.0);
                surfaceData.color = texSample.rgb * _UnlitColor.rgb * vColor.rgb;

                float texAlpha = texSample.a;
                if (_LuminanceToAlpha > 0.5)
                {
                    float lum = saturate(dot(texSample.rgb, float3(0.299, 0.587, 0.114)) * 1.5);
                    texAlpha = min(texAlpha, lum);
                }

                float alpha = lerp(_AlphaRemapMin, _AlphaRemapMax, texAlpha);
                alpha *= _UnlitColor.a * vColor.a;

                surfaceData.normalWS = 0.0;

                #ifdef _ALPHATEST_ON
                GENERIC_ALPHA_TEST(alpha, _AlphaCutoff);
                #endif

                ZERO_BUILTIN_INITIALIZE(builtinData);
                builtinData.opacity = saturate(alpha);

                #ifdef _ALPHATEST_ON
                builtinData.alphaClipTreshold = _AlphaCutoff;
                #endif

                #if defined(DEBUG_DISPLAY)
                builtinData.renderingLayers = GetMeshRenderingLayerMask();
                #endif

                #ifdef _EMISSIVE_COLOR_MAP
                builtinData.emissiveColor = SAMPLE_TEXTURE2D(_EmissiveColorMap, sampler_EmissiveColorMap, TRANSFORM_TEX(input.texCoord0.xy, _EmissiveColorMap)).rgb * _EmissiveColor;
                #else
                builtinData.emissiveColor = _EmissiveColor;
                #endif

                float3 emissiveRcpExposure = builtinData.emissiveColor * GetInverseCurrentExposureMultiplier();
                builtinData.emissiveColor = lerp(emissiveRcpExposure, builtinData.emissiveColor, _EmissiveExposureWeight);

                ApplyDebugToBuiltinData(builtinData);
            }

            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassForwardUnlit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}

