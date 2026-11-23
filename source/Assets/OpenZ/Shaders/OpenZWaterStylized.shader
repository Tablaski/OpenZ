// OpenZ/WaterStylized v3: clean stylised water for URP 17.3 (Unity 6.3, Forward+), one pass, SRP Batcher compatible.
// Same shader name and GUID as v2, so M_Water / M_Water_Harbour keep their links.
//
//  - colour by VERTICAL water depth from the camera depth texture (camera-independent): clear shallows that show the bed,
//    a 3-stop shallow -> mid -> deep gradient, opacity rising with depth; the bed seen through the water keeps its
//    luminance (texture, caustics, unit shadows) and takes the water's hue with depth: clear turquoise, never murky
//    olive in the warm desert light, never black, never glowing
//  - refraction: the full-resolution URP opaque texture, shifted by the ripple slope in METRES of bed (constant at every
//    zoom), faded to zero at the waterline, never picking anything above the water
//  - ripples: one tileable normal map (T_WaterRipple_N) sampled as two layers (size, strength, 50 deg apart); optional world
//    flow map (T_WaterFlow_L02): the ripples drift ALONG each river branch (two-phase flow, no stretching); low amplitude
//  - caustics on the bed: analytic (five travelling waves, brightness = 1 / |det J| of the refracted-ray map), projected at
//    the bed position reconstructed from depth, lit by the sun and the bed's own shadow, faded in at the shore and out with
//    depth and zoom, off at night
//  - shore: a thin broken foam line where the water meets the bed, units and piers (vertical depth), an optional sandy-
//    shallows lift so the painted wet band under the water reads as wet sand, and a soft waterline
//  - light: the body is lit like an albedo (ambient SH + sun N.L x shadow + lamps), never emissive, so it can never be
//    brighter than sand in the same light; sky reflection = ambient probe along the reflected ray x Fresnel; a sparse toon
//    sun glint (fades out at high zoom); lamps / headlights / explosions light the water and streak on it (Forward+ loop)
//  - level 18 (1.5): _RippleProc = the ripples are twelve travelling waves in three layers read through a slow distortion instead of
//    the repeating ripple map (no period, no lattice; calm and ruffled patches); _FireReflect = fires and lamps show in the water as a
//    broad broken reflection drawn out toward the camera. Both are 0 on every other material: those draw exactly as before
//  - 1.5 water_fx: _ProcDrift / _ProcDriftTurn = each wave layer drifts along the flow at its own speed, a little turned; _ProcMorph =
//    the small ripples slowly change shape (their phases run at their own rates). All 0 = the waves exactly as before
//  - URP fog on the surface part only (the bed seen through the water is already fogged in the opaque texture)
//  - night (_OZNight, NightGrade): darker desaturated body, no caustics, dim foam, physical moon glint, lamp streaks
// Needs the URP asset's Depth Texture ON and Opaque Texture ON with Opaque Downsampling NONE (OpenZ > Water v3 > Apply).
// Fallback keywords: _WATER_NO_OPAQUE (plain alpha blending, no refraction / tint), _WATER_NO_DEPTH (fixed depth).
Shader "OpenZ/WaterStylized"
{
    Properties
    {
        [Header(Colour by vertical water depth)]
        _ShallowColor ("Shallow colour", Color) = (0.377, 1, 1, 1)
        _MidColor ("Mid colour", Color) = (0.318, 1, 0.974, 1)
        _DeepColor ("Deep colour", Color) = (0, 0.717, 0.7, 1)
        _MidDepth ("Mid colour at depth (m)", Range(0.02, 3)) = 0.3
        _DeepDepth ("Deep colour at depth (m)", Range(0.05, 6)) = 0.72
        _OpacityShallow ("Opacity at the waterline", Range(0, 1)) = 0.2
        _OpacityDeep ("Opacity of deep water", Range(0, 1)) = 0.577
        _OpacityDepth ("Opacity depth (m to 63 %)", Range(0.02, 6)) = 0.1
        _BedTint ("Bed colour under the water (its luminance is kept)", Color) = (0.561, 0.96, 1, 1)
        _BedTintDepth ("Bed recolour depth (m to full)", Range(0.02, 6)) = 0.32
        _SandLift ("Sandy shallows: lift a dark bed toward the sand colour", Range(0, 1)) = 0.3
        _SandColor ("Sandy shallows colour (albedo)", Color) = (0.9, 0.8, 0.6, 1)
        _SandDepth ("Sandy shallows depth (m)", Range(0.02, 1)) = 0.35
        _Refraction ("Refraction (m of bed shift at full ripple slope)", Range(0, 0.5)) = 0.1

        [Header(Ripples)]
        [NoScaleOffset][Normal] _RippleNormal ("Ripple normal map (tileable)", 2D) = "bump" {}
        _RippleScaleA ("Layer A size (m per tile)", Range(0.5, 40)) = 6
        _RippleScaleB ("Layer B size (m per tile)", Range(0.5, 40)) = 2.6
        _RippleStrengthA ("Layer A strength", Range(0, 2)) = 0.32
        _RippleStrengthB ("Layer B strength", Range(0, 2)) = 0.2
        _RippleSpeed ("Drift speed (m per s)", Range(0, 2)) = 0.12
        _RippleShade ("Ripple shading on the body", Range(0, 1)) = 0.8
        _RippleContrast ("Ripple contrast from any view (v2 mottling, L17)", Range(0, 1.5)) = 0
        _ToneVar ("Large slow change of tone over the body (0 = off; level 18)", Range(0, 1)) = 0
        _ToneSize ("Tone change size (m)", Range(10, 300)) = 60
        _RippleProc ("Ripples from travelling waves instead of the ripple map (0 = the map, 1 = waves; level 18)", Range(0, 1)) = 0
        _ProcSize ("Waves: the smallest ripple, crest to crest (m); the two larger layers are 2.7 x and 7 x", Range(0.1, 3)) = 0.42
        _ProcSlope ("Waves: steepness of the small, the middle and the large layer (x, y, z)", Vector) = (0.16, 0.12, 0.08, 0)
        _ProcSpeed ("Waves: travel speed (x)", Range(0, 4)) = 1
        _ProcCalm ("Waves: calm patches between ruffled ones (0 = even everywhere)", Range(0, 1)) = 0.6
        _ProcCalmSize ("Waves: size of the calm and ruffled patches (m)", Range(5, 120)) = 24
        _ProcDrift ("Waves: drift of the small, the middle and the large layer along the flow (m per s; x, y, z)", Vector) = (0, 0, 0, 0)
        _ProcDriftTurn ("Waves: each layer's drift turned off the flow (deg; x, y, z)", Vector) = (0, 0, 0, 0)
        _ProcMorph ("Waves: slow change of the small ripples' shape (rad per s; 0 = none)", Range(0, 3)) = 0
        _FireReflect ("Fires and lamps: a broad broken reflection drawn out toward the camera (0 = off; level 18)", Range(0, 3)) = 0
        _SunLane ("The sun on the water: a broad broken lane toward a low sun (0 = off; level 18)", Range(0, 4)) = 0
        _SunLanePower ("Sun lane: how narrow", Range(8, 200)) = 46
        _SunLaneGlint ("Sun lane as glints: 0 = the broad lane (level 18), 1 = a narrow lane of glints, no broad wash, fading on steep views (level 17)", Range(0, 1)) = 0
        _FlowDirection ("Flow direction without a flow map (deg, 0 = +X, 90 = +Z)", Range(0, 360)) = 90
        [Toggle(_WATER_FLOWMAP)] _UseFlowMap ("Use the flow map", Float) = 0
        [NoScaleOffset] _FlowMap ("Flow map (RG = direction x speed, 0.5 = still)", 2D) = "gray" {}
        _FlowRect ("Flow map rect (x0, z0, size x, size z in m; a negative size mirrors)", Vector) = (0, 0, 1, 1)
        _FlowCycle ("Flow cycle (s)", Range(1, 30)) = 8

        [Header(Light and sky)]
        _AmbientStrength ("Ambient strength", Range(0, 2)) = 1
        _ShadowStrength ("Sun shadow strength on the water", Range(0, 1)) = 0.75
        _SkyColor ("Sky reflection tint", Color) = (0.85, 0.94, 1, 1)
        _ReflectionStrength ("Sky reflection strength", Range(0, 1)) = 0.6
        _ReflectionF0 ("Reflection facing the camera", Range(0, 0.3)) = 0.06
        _FresnelPower ("Fresnel power", Range(1, 10)) = 5
        _GlintPower ("Sun glint size (Blinn power)", Range(8, 4096)) = 900
        _GlintIntensity ("Sun glint intensity", Range(0, 4)) = 1
        _GlintThreshold ("Sun glint toon threshold", Range(0, 1)) = 0.45
        _GlintSoftness ("Sun glint softness", Range(0.001, 0.5)) = 0.08
        _GlintViewBias ("Sun glint view bias (0 = physical)", Range(0, 1)) = 0.3
        _GlintSparsity ("Sun glint sparsity", Range(0, 1)) = 0.55
        _GlintFadeHeight ("Glints fade out above camera height (m)", Range(20, 400)) = 150
        [ToggleOff(_WATER_NO_ADDITIONAL_LIGHTS)] _AdditionalLights ("Lamps, headlights and fires light the water", Float) = 1
        _AddLightStrength ("Lamp light strength", Range(0, 4)) = 1

        [Header(Caustics on the bed)]
        _CausticsStrength ("Caustics strength", Range(0, 3)) = 0.8
        _CausticsScale ("Caustics size", Range(0.2, 5)) = 1
        _CausticsFocus ("Caustics focus (line sharpness)", Range(0.05, 0.5)) = 0.24
        _CausticsSpeed ("Caustics speed", Range(0, 3)) = 0.6
        _CausticsSplit ("Caustics colour split", Range(0, 0.3)) = 0.06
        _CausticsFadeIn ("Caustics fade-in depth (m)", Range(0.001, 1)) = 0.08
        _CausticsFadeOut ("Caustics fade-out depth (m)", Range(0.1, 10)) = 1.6

        [Header(Shore)]
        _FoamColor ("Foam colour (A = opacity)", Color) = (0.9, 0.95, 0.92, 0.45)
        _FoamWidth ("Foam width (m of depth)", Range(0, 1)) = 0.06
        _FoamScale ("Foam break-up size (m)", Range(0.1, 10)) = 0.9
        _FoamSpeed ("Foam drift (m per s)", Range(0, 1)) = 0.05
        _FoamBreakup ("Foam break-up", Range(0, 1)) = 0.75
        _FoamSoftness ("Foam softness", Range(0.001, 0.5)) = 0.08
        [HideInInspector] _OzAiWaterV5 ("AI maps V5 water profile", Float) = 0
        _EdgeFade ("Waterline softening (m of depth)", Range(0.001, 0.3)) = 0.02
        [Header(Wrecks and swimmers in the water  kit water_depth_soft 1.5)]
        _ObjectClarity ("Object under the surface: m to 63 % of its opacity", Range(0.05, 3)) = 0.25
        _ObjectOpacityMax ("Object under the surface: max opacity", Range(0, 1)) = 0.95
        _ObjectEdgeFade ("Object waterline softening (m of depth)", Range(0.001, 0.3)) = 0.05
        _ContactRing ("Contact ring opacity (outline foam + ripple)", Range(0, 1)) = 0.5
        _ContactRingWidth ("Contact ring width (m)", Range(0.02, 0.6)) = 0.14
        _ContactRippleReach ("Contact ripple reach (m)", Range(0, 3)) = 0.8
        _ContactRippleSpeed ("Contact ripple rate (rings per s)", Range(0, 2)) = 0.35
        _ObjectClearBand ("Clear band round objects standing in the water (0 = off; level 18, fixes_r5)", Range(0, 1)) = 0
        _ObjectClearWidth ("Clear band width (m)", Range(0.1, 4)) = 1.2

        [Header(Night)]
        _NightDarken ("Night body darkening", Range(0, 1)) = 0.675
        _NightDesaturate ("Night body desaturation", Range(0, 1)) = 1

        [Header(Fallbacks)]
        [Toggle(_WATER_NO_OPAQUE)] _NoOpaque ("No opaque texture (plain blending, no refraction)", Float) = 0
        [Toggle(_WATER_NO_DEPTH)] _NoDepth ("No depth texture (fixed depth)", Float) = 0
        _FallbackDepth ("Fixed depth (m)", Range(0, 5)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "WaterForward"
            Tags { "LightMode"="UniversalForward" }

            Blend One OneMinusSrcAlpha      // premultiplied: the bed is composited in the shader, alpha = waterline fade
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WaterVert
            #pragma fragment WaterFrag

            #pragma shader_feature_local_fragment _WATER_FLOWMAP
            #pragma shader_feature_local_fragment _WATER_NO_OPAQUE
            #pragma shader_feature_local_fragment _WATER_NO_DEPTH
            #pragma shader_feature_local_fragment _WATER_NO_ADDITIONAL_LIGHTS

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            // transparent surface: main-light shadows come from the shadow map, never the opaque screen-space shadow texture
            #define _SURFACE_TYPE_TRANSPARENT 1

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "OpenZWaterContact.hlsl"                                       // water_depth_soft (1.5): wrecks, pieces and swimmers in the water
            #include "OpenZWaterBlast.hlsl"   // 1.5 kit water_blast_r6 (A16): the stamps a blast leaves in the water (View/OzWaterBlast.cs)
            #include "OzWaterObjReflect.hlsl"
            #include "OpenZWet.hlsl"                                                // weather_l17_v1: rain drop ripples (_OzRain, 0 = none)

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _MidColor;
                half4 _DeepColor;
                float _MidDepth;
                float _DeepDepth;
                half _OpacityShallow;
                half _OpacityDeep;
                float _OpacityDepth;
                half4 _BedTint;
                float _BedTintDepth;
                half _SandLift;
                half4 _SandColor;
                float _SandDepth;
                float _Refraction;
                float _RippleScaleA;
                float _RippleScaleB;
                half _RippleStrengthA;
                half _RippleStrengthB;
                float _RippleSpeed;
                half _RippleShade;
                half _RippleContrast;
                half _ToneVar;
                float _ToneSize;
                half _RippleProc;
                float _ProcSize;
                half4 _ProcSlope;
                float _ProcSpeed;
                half _ProcCalm;
                float _ProcCalmSize;
                float4 _ProcDrift;
                float4 _ProcDriftTurn;
                float _ProcMorph;
                half _FireReflect;
                half _SunLane;
                half _SunLanePower;
                half _SunLaneGlint;
                float _FlowDirection;
                half _UseFlowMap;
                float4 _FlowRect;
                float _FlowCycle;
                half _AmbientStrength;
                half _ShadowStrength;
                half4 _SkyColor;
                half _ReflectionStrength;
                half _ReflectionF0;
                half _FresnelPower;
                half _GlintPower;
                half _GlintIntensity;
                half _GlintThreshold;
                half _GlintSoftness;
                half _GlintViewBias;
                half _GlintSparsity;
                float _GlintFadeHeight;
                half _AdditionalLights;
                half _AddLightStrength;
                half _CausticsStrength;
                float _CausticsScale;
                half _CausticsFocus;
                float _CausticsSpeed;
                half _CausticsSplit;
                float _CausticsFadeIn;
                float _CausticsFadeOut;
                half4 _FoamColor;
                float _FoamWidth;
                float _FoamScale;
                float _FoamSpeed;
                half _FoamBreakup;
                half _FoamSoftness;
                float _OzAiWaterV5;
                float _EdgeFade;
                float _ObjectClarity; half _ObjectOpacityMax; float _ObjectEdgeFade; half _ContactRing; float _ContactRingWidth; float _ContactRippleReach; float _ContactRippleSpeed;   // water_depth_soft (1.5)
                half _ObjectClearBand; float _ObjectClearWidth;
                half _NightDarken;
                half _NightDesaturate;
                half _NoOpaque;
                half _NoDepth;
                float _FallbackDepth;
            CBUFFER_END
            // 1.5 kit water_tune_r3 (was R3.19's one _OZWaterCalm): set by View/OzWaterPlayCalm.cs only while the game camera plays (never in
            // briefings / flyovers); unset = 0 = as the material says. _OZWaterCalmA = how much weaker: x the sparse glints, y the sun lane,
            // z the sky reflection, w the fire / lamp reflection (R3.19's c was (c, c, c, c / 2)); _OZWaterCalmB.x = how much lower the ripples
            float4 _OZWaterCalmA;
            float4 _OZWaterCalmB;   // .y (1.5 kit l18_water_subtle_r6): each light's broad mirror glow (_FireReflect) is capped at 3.0 x (1 - y), level 18 in play (0 = the cap 3.0 as before)

            TEXTURE2D(_RippleNormal); SAMPLER(sampler_RippleNormal);
            TEXTURE2D(_FlowMap);      SAMPLER(sampler_FlowMap);

            float _OZNight;   // night package (NightGrade): 0 by day .. 1 at night, global
            // unset = 0 = every picture as before): x = the sun lane, y = the fire / lamp reflection, z = the clear band round objects standing in
            // the water (level 18), w = free (a later object reflection reads it). A part is skipped only while its component is 1.
            float4 _OZWaterLite;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  ambientUp  : TEXCOORD1;    // SH ambient for the flat, up-facing surface
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // layer B turns 50 deg against layer A (no visible repeat where the two tiles line up)
            static const float2x2 kRotB = float2x2(0.6427876, -0.7660444, 0.7660444, 0.6427876);
            static const float2x2 kRotC = float2x2(0.7986355, -0.6018150, 0.6018150, 0.7986355);   // NB46 anti-tiling copy of layer A (37 deg)
            // caustics: five travelling waves (wave vector in rad per m, amplitude = 1 / |k|, phase, angular speed)
            static const float2 kCausK[5] = { float2(2.8617, 0.8852), float2(-1.3055, 3.8360), float2(-4.6700, -2.3303),
                                              float2(1.8702, -6.3448), float2(-4.2261, 1.8930) };
            static const float kCausA[5] = { 0.3339, 0.2468, 0.1917, 0.1512, 0.2159 };
            static const float kCausP[5] = { 0.61, 2.93, 4.47, 1.19, 5.71 };
            static const float kCausW[5] = { 1.894, 2.203, 2.500, 2.816, 2.395 };

            float WaterHash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float WaterValueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(WaterHash21(i), WaterHash21(i + float2(1, 0)), u.x),
                            lerp(WaterHash21(i + float2(0, 1)), WaterHash21(i + float2(1, 1)), u.x), u.y);
            }

            // world position of the opaque scene behind a screen position (perspective and orthographic)
            float3 WaterScenePosition(float3 positionWS, float2 screenUV)
            {
                float rawDepth = SampleSceneDepth(screenUV);
                float surfaceEye = -TransformWorldToView(positionWS).z;
                if (IsPerspectiveProjection())
                {
                    float3 camPos = GetCameraPositionWS();
                    float sceneEye = LinearEyeDepth(rawDepth, _ZBufferParams);
                    return camPos + (positionWS - camPos) * (sceneEye / max(surfaceEye, 1e-4));
                }
                float sceneEyeOrtho = LinearDepthToEyeDepth(rawDepth);
                return positionWS + GetViewForwardDir() * (sceneEyeOrtho - surfaceEye);
            }

            // flow velocity in m per s, world XZ
            float2 WaterFlowVelocity(float2 xz)
            {
            #if defined(_WATER_FLOWMAP)
                // a negative size mirrors the map on that axis (README step 4): keep the sign and turn the stored
                float2 sgn = step(0.0, _FlowRect.zw) * 2.0 - 1.0;
                float2 uv = (xz - _FlowRect.xy) / (sgn * max(abs(_FlowRect.zw), 1e-3));
                float2 f = (SAMPLE_TEXTURE2D_LOD(_FlowMap, sampler_FlowMap, uv, 0).rg * 2.0 - 1.0) * sgn;
                return f * _RippleSpeed;
            #else
                float a = radians(_FlowDirection);
                return float2(cos(a), sin(a)) * _RippleSpeed;
            #endif
            }

            // both ripple layers at a displacement -> world-space slope (x, z)
            half2 WaterRippleSlope(float2 xz, float2 disp)
            {
                float2 pA = (xz - disp) / _RippleScaleA;
                half3 nA = UnpackNormalScale(SAMPLE_TEXTURE2D(_RippleNormal, sampler_RippleNormal, pA), _RippleStrengthA);
                // turned 37 deg at 1.41x scale, blended in by a slow noise, and a slow strength variation, so no 8 m period reads
                float2 pA2 = mul(kRotC, xz - disp * 0.85) / (_RippleScaleA * 1.41) + float2(0.13, 0.59);
                half3 nA2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_RippleNormal, sampler_RippleNormal, pA2), _RippleStrengthA);
                half wA = (half)smoothstep(0.3, 0.7, WaterValueNoise(xz * 0.045 + 11.3));
                half gA = (half)(0.65 + 0.7 * WaterValueNoise(xz * 0.028 + 3.7));
                nA.xy = lerp(nA.xy, mul(nA2.xy, kRotC), wA) * gA;
                float2 pB = mul(kRotB, xz - disp * 0.7) / _RippleScaleB + float2(0.37, 0.71);
                half3 nB = UnpackNormalScale(SAMPLE_TEXTURE2D(_RippleNormal, sampler_RippleNormal, pB), _RippleStrengthB);
                return nA.xy + mul(nB.xy, kRotB);      // layer B's tangent frame turned back to world
            }

            // picture that repeats but twelve travelling waves in three layers: the smallest _ProcSize from crest to crest, the others 2.7 x and
            // 7 x (never a whole multiple), each layer turned to its own direction round the flow and moving at its own speed, four waves of
            // slightly different length and direction in each, all read through a slow large distortion: there is no period and no lattice.
            // The two finer layers are ruffled in patches and calm between them (_ProcCalm); a wave shorter than about three pixels fades out
            // (no shimmer at the widest zoom). Returns the world-space slope (x, z).
            static const float kProcDir[12] = { -0.82, -0.19, 0.44, 1.03,    1.21, 0.31, -0.35, -1.12,    -0.71, 0.58, -0.12, 0.97 };   // radians off the flow
            static const float kProcLen[12] = { 1.00, 0.83, 1.21, 0.69,    2.70, 2.26, 3.19, 1.93,    7.00, 5.71, 8.43, 4.87 };          // x _ProcSize
            static const float kProcPh[12]  = { 0.0, 1.7, 3.9, 5.2,    2.3, 4.6, 0.9, 5.9,    1.1, 3.3, 5.5, 0.4 };
            static const float kProcMorph[12] = { 1.00, -0.73, 0.61, -1.17,    0.50, -0.37, 0.31, -0.58,    0.0, 0.0, 0.0, 0.0 };   // x _ProcMorph (rad per s): the small layer, half for the middle one

            half2 WaterProcSlope(float2 xz, float t, float2 flowDir, float mPerPx)
            {
                float2 w = (float2(WaterValueNoise(xz * 0.09 + float2(3.1, 7.7) + t * 0.013), WaterValueNoise(xz * 0.09 + float2(11.9, 1.3) - t * 0.011)) - 0.5) * 11.0;
                w += (float2(WaterValueNoise(xz * 0.31 + float2(5.3, 2.9) - t * 0.021), WaterValueNoise(xz * 0.31 + float2(8.2, 6.1) + t * 0.017)) - 0.5) * 3.2;
                w += (float2(WaterValueNoise(xz * 0.93 + float2(2.2, 4.4)), WaterValueNoise(xz * 0.93 + float2(9.1, 0.7))) - 0.5) * 0.9;
                float2 p = xz + w * _ProcSize;
                float patches = 0.62 * WaterValueNoise(xz / max(_ProcCalmSize, 1.0) + float2(1.7, 9.2) + flowDir * (t * 0.02))
                              + 0.38 * WaterValueNoise(xz / max(_ProcCalmSize * 0.41, 1.0) + float2(6.4, 2.5) - flowDir * (t * 0.03));
                half ruffle = lerp((half)1.0, (half)(0.12 + 1.45 * smoothstep(0.3, 0.7, patches)), _ProcCalm);
                float base = atan2(flowDir.y, flowDir.x);
                // each layer drifts along the flow at its own speed, a little turned off it (all 0: no drift, the same sums as before)
                float3 dTurn = radians(_ProcDriftTurn.xyz);
                float2 dr0 = float2(cos(base + dTurn.x), sin(base + dTurn.x)) * (_ProcDrift.x * t);
                float2 dr1 = float2(cos(base + dTurn.y), sin(base + dTurn.y)) * (_ProcDrift.y * t);
                float2 dr2 = float2(cos(base + dTurn.z), sin(base + dTurn.z)) * (_ProcDrift.z * t);
                // wave groups: one slow noise per layer (its cells about five of the layer's waves wide, drifting with the flow)
                float3 grp = float3(WaterValueNoise(xz / (_ProcSize * 5.0) + float2(4.9, 1.2) - flowDir * (t * 0.06)),
                                    WaterValueNoise(xz / (_ProcSize * 13.5) + float2(0.6, 8.3) - flowDir * (t * 0.035)),
                                    WaterValueNoise(xz / (_ProcSize * 35.0) + float2(7.7, 3.4) - flowDir * (t * 0.02)));
                half2 slope = half2(0.0, 0.0);
                [unroll] for (int i = 0; i < 12; i++)
                {
                    float len = kProcLen[i] * _ProcSize;
                    float k = 6.2831853 / len;
                    float a = base + kProcDir[i];
                    float2 dir = float2(cos(a), sin(a));
                    float2 q = p - (i < 4 ? dr0 : (i < 8 ? dr1 : dr2));
                    float ph = dot(dir, q) * k - (0.35 * sqrt(len) * _ProcSpeed) * k * t + kProcPh[i] + _ProcMorph * kProcMorph[i] * t;
                    half amp = i < 4 ? _ProcSlope.x * ruffle : (i < 8 ? _ProcSlope.y * ruffle : _ProcSlope.z);
                    float g = i < 4 ? grp.x : (i < 8 ? grp.y : grp.z);
                    amp *= (half)(0.25 + 1.5 * saturate(0.5 + 0.5 * sin(6.2831853 * (g * 1.7 + (float)(i % 4) * 0.25))));   // each of the four waves has its own share of the groups
                    half aa = (half)saturate((len / mPerPx - 2.5) * 0.4);
                    // a crest is narrower than a trough (the second harmonic): ripples, not round lumps
                    slope += (half2)(dir * ((cos(ph) + 0.35 * cos(2.0 * ph + 0.6)) * amp * 0.5 * aa));
                }
                return slope;
            }

            half2 WaterRipples(float2 xz, float t, float2 vel)
            {
            #if defined(_WATER_FLOWMAP)
                // two-phase flow (the ripples follow each branch without stretching): two copies half a cycle apart,
                // each cross-fading out while it resets; a slow noise offsets the phase so the reset never pulses in sync
                float cyc = max(_FlowCycle, 0.5);
                float ph = t / cyc + WaterValueNoise(xz * 0.21) * 0.5;
                float p0 = frac(ph), p1 = frac(ph + 0.5);
                half w1 = (half)abs(1.0 - 2.0 * p0);
                half2 s0 = WaterRippleSlope(xz, vel * ((p0 - 0.5) * cyc));
                half2 s1 = WaterRippleSlope(xz + float2(2.3, 1.7), vel * ((p1 - 0.5) * cyc));
                return lerp(s0, s1, w1);
            #else
                return WaterRippleSlope(xz, vel * t);
            #endif
            }

            // analytic caustics: bed brightness = 1 / |det(I - D * Hessian(h))| of the refracted-ray map, per colour channel
            half3 WaterCaustics(float2 p, float t, half focus, half split, half aa)
            {
                float hxx = 0.0, hzz = 0.0, hxz = 0.0;
                [unroll] for (int w = 0; w < 5; w++)
                {
                    float2 k = kCausK[w];
                    float s = sin(dot(k, p) - kCausW[w] * t + kCausP[w]) * kCausA[w];
                    hxx -= k.x * k.x * s;
                    hzz -= k.y * k.y * s;
                    hxz -= k.x * k.y * s;
                }
                float3 D = focus * float3(1.0 + split, 1.0, 1.0 - split);
                float3 J = (1.0 - D * hxx) * (1.0 - D * hzz) - D * D * (hxz * hxz);
                float3 I = 1.0 / max(abs(J), 0.12 + aa);
                return (half3)saturate((I - 1.0) * 0.22);
            }

            Varyings WaterVert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.ambientUp = SampleSH(half3(0.0, 1.0, 0.0));
                return o;
            }

            half4 WaterFrag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 positionWS = i.positionWS;
                float2 xz = positionWS.xz;
                float t = _Time.y;
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
                const half3 up = half3(0.0, 1.0, 0.0);
                half night = saturate((half)_OZNight);
                float mPerPx = max(length(fwidth(xz)), 1e-5);                         // metres per pixel on the surface

                // ---- the scene under this pixel: vertical depth drives colour, opacity, foam and caustics
                float d;
                float3 sceneWS;
            #if defined(_WATER_NO_DEPTH)
                d = _FallbackDepth;
                sceneWS = positionWS - float3(0.0, _FallbackDepth, 0.0);
            #else
                sceneWS = WaterScenePosition(positionWS, screenUV);
                d = max(positionWS.y - sceneWS.y, 0.0);
            #endif
                half edge = (half)saturate(d / max(_EdgeFade, 1e-4));
                // water_depth_soft (1.5): over a wreck, a piece or a swimmer (inside its waterline outline, the scene within its depth) the part under
                // the surface shows faintly (the harbour starts at 75 % opacity at the waterline: a hull looked cut off on a solid surface)
                half2 wc = OzWaterContacts(positionWS, d, t, mPerPx, _ContactRingWidth, _ContactRippleReach, _ContactRippleSpeed);
                half3 wkH = OzWaterWakeHull(positionWS, d);   // 1.5 kit bizon_ripple_r6 (A2): the wading hull: x over its submerged part, y its soft clear zone, z its flanks; no Bizon in water = 0
                half objK = max(wc.y, wkH.x);   // 1.5 kit bizon_ripple_r6 (A2): the water over the submerged hull is as clear as over a wreck (no ring comes with it)
                edge = (half)saturate(d / max(lerp(_EdgeFade, _ObjectEdgeFade, (float)objK), 1e-4));

                // ---- ripples -> normal
                float2 vel = WaterFlowVelocity(xz);
                half2 slope;
                if (_RippleProc > 0.5)
                {
                    float vl = length(vel);
                    slope = WaterProcSlope(xz, t, vl > 1e-5 ? vel / vl : float2(1.0, 0.0), mPerPx);
                }
                else slope = WaterRipples(xz, t, vel);   // kit l18_perf_r3: the ripple map only where it is used (it was worked out and dropped under the waves)
                slope *= (half)(1.0 - _OZWaterCalmB.x);   // kit water_tune_r3: lower ripples / waves in play (level 18 35 %, level 17 40 %; rain rings kept)
                if (_OzRain > 0.001) slope += OZWRipples(xz, t, (half)_OzRain, mPerPx) * (half)1.6;   // weather_l17_v1: rain drop rings
                half4 wb = OzWaterBlast(positionWS, t, mPerPx);   // 1.5 kit water_blast_r6 (A16): a blast in the water: xy the slope of its spreading crests, z churn, w the blast hole; no stamp alive = 0
                slope = slope * (half)(1.0 - 0.6 * max(wb.z, wb.w)) + wb.xy;   // 1.5 kit water_blast_r6 (A16): churned water and the blast hole have calmer small waves; no stamp = exactly as before
                slope += OzWaterContactSlope();   // 1.5 kit water_rings_rest_r6: the ripples round wrecks, pieces, swimmers and dipping hulls as the water's own slope (open arcs; no pale ring line); none near = + 0
                half3 wk = OzWaterWake(positionWS, t, mPerPx);   // 1.5 kit bizon_ripple_r6 (A2): the ripples a wading hull leaves in the water (slope) and its churn (z)
                slope = slope * (half)(1.0 - 0.6 * wk.z) + wk.xy;   // 1.5 kit bizon_ripple_r6 (A2): churned water has calmer small waves; no stamp = exactly as before
                half3 n = normalize(half3(slope.x, 1.0, slope.y));
                half3 V = GetWorldSpaceNormalizeViewDir(positionWS);

                // ---- main light (the moon at night) on the surface
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
                half3 sunColor = mainLight.color * mainLight.distanceAttenuation;
                half3 L = mainLight.direction;
                half sunShadow = lerp(1.0 - _ShadowStrength, 1.0, mainLight.shadowAttenuation);
                half3 ambient = i.ambientUp * _AmbientStrength;
                half3 bodyN = normalize(lerp(up, n, _RippleShade));
                half3 diffuse = ambient + sunColor * (saturate(dot(bodyN, L)) * sunShadow);

                // ---- lamps, headlights, muzzle flashes, explosions (Forward+ sets only _CLUSTER_LIGHT_LOOP)
                half3 addSpec = half3(0.0, 0.0, 0.0);
            #if (defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP)) && !defined(_WATER_NO_ADDITIONAL_LIGHTS)
                {
                    InputData inputData = (InputData)0;          // LIGHT_LOOP_BEGIN reads inputData in the cluster loop
                    inputData.positionWS = positionWS;
                    inputData.normalWS = n;
                    inputData.viewDirectionWS = V;
                    inputData.normalizedScreenSpaceUV = screenUV;
                    uint lightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light light = GetAdditionalLight(lightIndex, positionWS);
                        half3 radiance = light.color * (light.distanceAttenuation * light.shadowAttenuation * _AddLightStrength);
                        // crumpled bright paper in every lamp pool: the body takes the lamp on a nearly flat normal (water mostly reflects,
                        // so less of it) and the streak lobe is tighter and weaker: clean glints on the ripples instead of blotches
                        half3 lampN = normalize(lerp(up, n, (half)0.12));
                        diffuse += radiance * (saturate(dot(lampN, light.direction)) * (half)0.55);
                        half3 hAdd = SafeNormalize(float3(light.direction) + float3(V));
                        half3 nStreak = normalize(half3(n.x * 2.2, 1.0, n.z * 2.2));   // lamp reflections break into streaks
                        addSpec += radiance * (pow(saturate(dot(n, hAdd)), _GlintPower * 0.25)
                                   + (0.12 + 0.22 * night) * pow(saturate(dot(nStreak, hAdd)), 140.0)) * _GlintIntensity;
                        if (_FireReflect > 0.0 && _OZWaterLite.y < 0.5)   // kit l18_perf_r3: off on GRAPHICS QUALITY LOW
                        {
                            // moving reflection: the ripples' slope counts five times along the line of sight and 1.6 times across it, so the
                            // glints spread into a path toward the camera, strongest under the light and gone where its light ends
                            half2 vd = SafeNormalize(float3(V.x, 0.0, V.z)).xz;
                            half sAlong = dot(n.xz, vd);
                            half2 sPath = vd * (sAlong * 5.0) + (n.xz - vd * sAlong) * 1.6;
                            half3 nPath = normalize(half3(sPath.x, 1.0, sPath.y));
                            // a mirror picture of the glowing thing, not its light on the water: the light's own strength inside its range
                            // (its fall-off with the square of the distance taken out again), so a fire shows from far and a weak lamp stays faint
                        #if defined(_CLUSTER_LIGHT_LOOP)
                        #if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
                            float3 toLight = _AdditionalLightsBuffer[lightIndex].position.xyz - positionWS;
                        #else
                            float3 toLight = _AdditionalLightsPosition[lightIndex].xyz - positionWS;
                        #endif
                            half inRange = (half)saturate(light.distanceAttenuation * dot(toLight, toLight));
                        #else
                            float3 toLight = light.direction * 5.0;                                 // without the light list: as if every light stood 5 m away
                            half inRange = (half)saturate(light.distanceAttenuation * 25.0);
                        #endif
                            half3 glow = min(light.color * (inRange * light.shadowAttenuation * (half)0.07 / (1.0 + dot(toLight, toLight) / 350.0)), half3(3.0, 3.0, 3.0));   // half at about 19 m from the fire
                            glow *= min((half)1.0, (half)(3.0 * (1.0 - _OZWaterCalmB.y)) / max(max(glow.r, glow.g), max(glow.b, (half)0.0001)));   // kit l18_water_subtle_r6: in play on level 18 one light shows at most 3.0 x (1 - y) (colour kept): lamps, blasts and lightning come down to a fire's level, a fire stays; y = 0 = x 1.0
                            addSpec += glow * ((pow(saturate(dot(nPath, hAdd)), 18.0) + pow(saturate(dot(up, hAdd)), 3.0) * 0.06) * _FireReflect);
                        }
                    LIGHT_LOOP_END
                }
            #endif

                // ---- water body: 3-stop colour and opacity by depth, lit like an albedo (never emissive)
                half tMid = (half)smoothstep(0.0, max(_MidDepth, 1e-3), d);
                half tDeep = (half)smoothstep(_MidDepth, max(_DeepDepth, _MidDepth + 1e-3), d);
                half3 bodyAlb = lerp(lerp(_ShallowColor.rgb, _MidColor.rgb, tMid), _DeepColor.rgb, tDeep);
                half bodyLum = dot(bodyAlb, half3(0.2126, 0.7152, 0.0722));
                bodyAlb = lerp(bodyAlb, bodyLum.xxx, _NightDesaturate * night) * (1.0 - _NightDarken * night);
                // metres, so no stretch of water has one flat tone; _ToneVar 0 (every other level) multiplies by exactly 1
                bodyAlb *= (half)lerp(1.0, 0.70 + 0.60 * (0.6 * WaterValueNoise(positionWS.xz / max(_ToneSize, 1.0) + 7.3) + 0.4 * WaterValueNoise(positionWS.xz / max(_ToneSize * 0.37, 1.0) + 19.1)), _ToneVar);
                half A = lerp(_OpacityShallow, _OpacityDeep, (half)(1.0 - exp(-d / max(_OpacityDepth, 1e-3))));
                A = lerp(A, min(A, _ObjectOpacityMax * (half)(1.0 - exp(-d / max(_ObjectClarity, 1e-3)))), objK);   // water_depth_soft: the part under the surface shows faintly
                if (_ObjectClearBand > 0.0 && _OZWaterLite.z < 0.5) A *= (half)(1.0 - _ObjectClearBand * OzWaterClearBand(positionWS, _ObjectClearWidth));
                if (_ObjectClearBand > 0.0 && _OZWaterLite.z < 0.5) A *= (half)(1.0 - _ObjectClearBand * wkH.y);   // 1.5 kit bizon_ripple_r6 (A2): the same clearer water round the wading hull, as a soft zone without an edge
                // optional view-independent ripple contrast (the v2 mottling the approved L17 harbour has; 0 on L02)
                half2 shadeDir = SafeNormalize(float3(L.x + 0.3, 0.0, L.z + 0.5)).xz;
                half rippleK = max(1.0 + dot(n.xz, shadeDir) * (_RippleContrast * 2.0), 0.0);
                half3 body = bodyAlb * diffuse * rippleK;

                // ---- sky reflection (ambient probe along the reflected ray) x Fresnel
                half NdotV = saturate(dot(n, V));
                half fres = (_ReflectionF0 + (1.0 - _ReflectionF0) * pow(1.0 - NdotV, _FresnelPower)) * _ReflectionStrength * (half)(1.0 - _OZWaterCalmA.z);   // kit water_tune_r3: z = the sky reflection
                half3 sky = SampleSH(reflect(-V, n)) * _SkyColor.rgb * _AmbientStrength;

                // ---- shore foam: a thin broken line where the water meets the bed, units and piers
                float2 fp = (xz - vel * (t * (_FoamSpeed / max(_RippleSpeed, 1e-3)))) / max(_FoamScale, 0.05);
                half fn = (half)(WaterValueNoise(fp) * 0.65 + WaterValueNoise(fp * 2.3 + 5.1) * 0.35);
                half band = (half)(1.0 - saturate(d / max(_FoamWidth, 1e-4)));
                half cut = lerp((half)0.5, (half)(0.3 + 0.85 * fn), _FoamBreakup);
                half fAA = (half)fwidth(band - cut) + _FoamSoftness;
                half foam = smoothstep(cut - fAA, cut + fAA, band) * _FoamColor.a * (1.0 - 0.5 * night) * (half)(_FoamWidth > 0.0);
                foam = max(foam, wc.x * _ContactRing * (half)(0.55 + 0.45 * fn) * (1.0 - 0.5 * night) * (1.0 - objK));   // water_depth_soft: thin foam + ripple ring where it cuts the surface
                [branch] if (wb.z + wb.w > 0.002)   // 1.5 kit water_blast_r6 (A16): foam flecks on the churned water (the foam colour through moving noise: the patch erodes as it dies) and the dark blast hole
                {
                    half wbN = (half)(0.6 * WaterValueNoise(xz * 2.3 + float2(0.09, -0.06) * t) + 0.4 * WaterValueNoise(xz * 5.7 - float2(0.04, 0.11) * t));
                    half wbCut = (half)(0.92 - 0.62 * wb.z);
                    foam = max(foam, smoothstep(wbCut - 0.08, wbCut + 0.08, wbN) * saturate(wb.z * 2.2) * _FoamColor.a * (half)0.9 * (1.0 - 0.5 * night));
                    body = lerp(body, _ShallowColor.rgb * diffuse, (half)0.35 * wb.z * wbN);   // churned water carries air: a little lighter, uneven
                    body *= (half)(1.0 - 0.45 * wb.w);   // the blast hole: darker for half a second
                }
                foam *= (half)(1.0 - wkH.z);   // 1.5 kit bizon_ripple_r6 (A2): no pale shore-foam line along the wading hull's flanks (OzWaterWakeHull z)
                [branch] if (wk.z > 0.002)   // 1.5 kit bizon_ripple_r6 (A2): a few foam flecks on churned water (the foam colour through moving noise; they erode as the patch dies)
                {
                    half wkN = (half)(0.6 * WaterValueNoise(xz * 2.7 + float2(0.11, -0.07) * t) + 0.4 * WaterValueNoise(xz * 6.1 - float2(0.05, 0.13) * t));
                    half wkCut = (half)(0.95 - 0.55 * wk.z);
                    foam = max(foam, smoothstep(wkCut - 0.07, wkCut + 0.07, wkN) * saturate(wk.z * 2.5) * _FoamColor.a * (half)0.85 * (1.0 - 0.5 * night));
                    body = lerp(body, _ShallowColor.rgb * diffuse, (half)0.3 * wk.z * wkN);   // churned water carries air: a little lighter, uneven
                }
                half3 foamLit = _FoamColor.rgb * diffuse;

                // ---- sparse toon sun glint (view-biased by day so the fixed RTS camera sees some; physical at night)
                half3 mirrorDir = reflect(-V, up);
                half3 glintL = SafeNormalize(lerp(float3(L), float3(mirrorDir), _GlintViewBias * (1.0 - night)));
                half3 H = SafeNormalize(float3(glintL) + float3(V));
                half specRaw = pow(saturate(dot(n, H)), _GlintPower);
                half specAA = (half)fwidth(specRaw);
                half glintThr = lerp(_GlintThreshold, max(_GlintThreshold, (half)0.85), night);
                half glint = smoothstep(glintThr - _GlintSoftness - specAA, glintThr + _GlintSoftness + specAA, specRaw);
                half patches = (half)WaterValueNoise(xz * 0.13 + vel * (t * 0.35));
                glint *= smoothstep(_GlintSparsity, _GlintSparsity + 0.2, patches);
                float camH = _WorldSpaceCameraPos.y - positionWS.y;
                glint *= (half)(1.0 - smoothstep(_GlintFadeHeight * 0.6, _GlintFadeHeight, camH));
                glint *= saturate(L.y * 4.0) * mainLight.shadowAttenuation;
                half3 sunLane = half3(0.0, 0.0, 0.0);
                if (_SunLane > 0.0 && _OZWaterLite.x < 0.5)   // kit l18_perf_r3: off on GRAPHICS QUALITY LOW
                {
                    // warm broken lane toward it: the ripples' slope counts five times along the line of sight and 1.6 times across it (the fire
                    // reflection's own maths), so the glints draw out into a path; sparkles travel through it with the waves
                    half2 svd = SafeNormalize(float3(V.x, 0.0, V.z)).xz;
                    half sAl = dot(n.xz, svd);
                    half2 sP = svd * (sAl * 5.0) + (n.xz - svd * sAl) * 1.6;
                    half3 nLane = normalize(half3(sP.x, 1.0, sP.y));
                    half3 hSun = SafeNormalize(float3(L) + float3(V));
                    // kit l17_sun_lane_glints (lead row L2): with _SunLaneGlint 1 the lane is narrower (power x4), has no broad wash (the second
                    // term), lights only where a sparkle is (no 0.35 floor under the whole lane) and fades on steep views (the RTS camera looks
                    // nearly straight down: a real sun path lies on grazing water), so the water's mean colour stays put and only glitter shows
                    half gl = _SunLaneGlint;
                    half lane = pow(saturate(dot(nLane, hSun)), _SunLanePower * (1.0 + 3.0 * gl)) + pow(saturate(dot(up, hSun)), _SunLanePower * 0.25) * (0.10 * (1.0 - gl));
                    half spark = (half)WaterValueNoise(xz * 2.1 + vel * (t * 1.6));
                    half sparkBroad = (half)(0.35 + 1.5 * smoothstep(0.45, 0.8, spark));
                    half sparkGlint = (half)(4.0 * smoothstep(0.70, 0.90, spark));
                    lane *= lerp(sparkBroad, sparkGlint, gl);
                    lane *= lerp((half)1.0, (half)(0.25 + 0.75 * smoothstep(0.97, 0.55, V.y)), gl);
                    sunLane = sunColor * (lane * _SunLane * saturate(L.y * 8.0 + 0.25) * (1.0 - night) * mainLight.shadowAttenuation);
                }
                // kit water_tune_r3: one calm per channel (x glints, y sun lane, w fire / lamp reflection)
                half3 S = (sunColor * (glint * _GlintIntensity) * (half)(1.0 - _OZWaterCalmA.x) + sunLane * (half)(1.0 - _OZWaterCalmA.y) + addSpec * (half)(1.0 - _OZWaterCalmA.w)) * (1.0 - foam);

                // ---- surface = what the water itself sends to the camera (premultiplied), trans = how much bed shows
                half cover = 1.0 - (1.0 - A) * (1.0 - fres);
                half3 surf = body * (A * (1.0 - fres)) + sky * fres;
                surf = surf * (1.0 - foam) + foamLit * foam;
                cover = cover * (1.0 - foam) + foam;
                if (_OzAiWaterV5 > .5)
                {
                    float air = WaterValueNoise(xz * float2(.13,.27) + float2(t * .013,-t * .009));
                    float ribbons = WaterValueNoise(xz * float2(.056,.15) + air * .38);
                    half veil = smoothstep(.43,.75,ribbons) * smoothstep(.10,.75,d) * (1 - foam) * (1 - .6 * night);
                    veil *= .086 * (.86 + .14 * (1 - V.y));
                    surf = lerp(surf,half3(.41,.60,.71) * diffuse,veil);
                    cover = lerp(cover,1,veil);
                    S *= 1 - veil * 1.3;
                }
                half trans = 1.0 - cover;

                // ---- fog on the surface part only: MixFog(1) - MixFog(0) = how much of the surface survives
                half fogFactor = InitializeInputDataFog(float4(positionWS, 1.0), 0.0);
                half fogVis = MixFog(half3(1.0, 1.0, 1.0), fogFactor).r - MixFog(half3(0.0, 0.0, 0.0), fogFactor).r;
                surf = surf * fogVis + unity_FogColor.rgb * ((1.0 - fogVis) * cover);
                S *= fogVis;

            #if defined(_WATER_NO_OPAQUE)
                return half4((surf + S) * edge, cover * edge);
            #else
                // ---- the bed: refracted (metres of shift -> screen UV at this depth), never picking anything above the water
                float surfaceEye = max(-TransformWorldToView(positionWS).z, 1e-3);
                float2 nView = TransformWorldToViewDir(float3(n.x, 0.0, n.z), false).xy;
                float refrM = _Refraction * saturate(d / 0.3);
                float2 uvR = screenUV + nView * refrM * float2(UNITY_MATRIX_P._m00, UNITY_MATRIX_P._m11) * (0.5 / surfaceEye);
                float3 bedWS = sceneWS;
            #if !defined(_WATER_NO_DEPTH)
                float3 behind = WaterScenePosition(positionWS, uvR);
                if (behind.y > positionWS.y - 0.01) uvR = screenUV; else bedWS = behind;
            #endif
                half3 bed = SampleSceneColor(uvR);
                float dBed = max(positionWS.y - bedWS.y, 0.0);

                // the bed's own sun light (flat-bed approximation) and shadow: caustics and sandy shallows follow it
                half bedShadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(bedWS));
                half3 bedSun = sunColor * (saturate(L.y) * bedShadow);

                // sandy shallows: only lifts a bed darker than wet sand in the same light (the painted wet band), zero at
                // the waterline (no seam against the dry bank) and past _SandDepth
                half liftK = _SandLift * (half)(smoothstep(0.0, 0.06, dBed) * (1.0 - smoothstep(_SandDepth * 0.5, _SandDepth, dBed)));
                half3 sandLit = _SandColor.rgb * (ambient + bedSun);
                bed = lerp(bed, max(bed, sandLit), liftK);

                // caustics: sun only, faded in at the shore and out with depth, calmed at high zoom, off at night
                half caFade = (half)(saturate(dBed / max(_CausticsFadeIn, 1e-3)) * (1.0 - saturate(dBed / max(_CausticsFadeOut, 1e-3))));
                caFade *= (half)(1.0 - smoothstep(0.06, 0.16, mPerPx)) * (1.0 - night) * saturate(L.y * 3.0) * bedShadow;
                if (caFade > 0.001)
                {
                    float2 cp = (bedWS.xz - vel * (t * 0.5)) / max(_CausticsScale, 0.05);
                    half aa = (half)saturate(mPerPx / max(_CausticsScale, 0.05) * 3.0);
                    half3 ca = WaterCaustics(cp, t * _CausticsSpeed, _CausticsFocus, _CausticsSplit, aa);
                    half sunLum = max(dot(sunColor, half3(0.2126, 0.7152, 0.0722)), 1e-3);
                    bed += bed * ca * (_CausticsStrength * caFade) * saturate(sunColor / sunLum);
                }

                // the bed seen through the water takes the water's hue: its luminance (texture, caustics, unit shadows) is
                // kept and recoloured by _BedTint with depth. A plain multiply would turn the warm tan bed murky olive in
                // the game's warm light; this stays clear and turquoise, and can never go black or glow (luminance kept,
                // scaled by the tint's own brightness)
                half bedLum = dot(bed, half3(0.2126, 0.7152, 0.0722));
                half3 bedWater = bedLum * _BedTint.rgb;
                half3 bedSeen = lerp(bed, bedWater, (half)saturate(dBed / max(_BedTintDepth, 1e-3)));
                half3 outC = surf + S + bedSeen * trans;
                // (_OZWaterObjRefl, View/OzWaterPlayCalm.cs), off on GRAPHICS QUALITY LOW (_OZWaterLite.w); unset = 0 = as before
                if (_OZWaterObjRefl.x > 0.0 && _OZWaterLite.w < 0.5)
                {
                    half4 ro = OzWaterObjReflect(positionWS, n, V, screenUV);
                    outC = lerp(outC, ro.rgb * (half)0.9, ro.a * (half)_OZWaterObjRefl.x * (1.0 - foam) * fogVis);
                }
                return half4(outC * edge, edge);
            #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
