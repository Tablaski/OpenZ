// OpenZ/ShockOverlay: the shock ring of the nuke and of the fort blast (OpenZFortShockwave.Overlay.cs), version 1.3 rows 45 and 46.
// One pass, additive (Blend One One), no keywords of its own (fog as every effect), no textures: nothing a player build can strip.
//  - drawn over everything with ZTest Always on one FLAT annulus at the blast's floor height: no hill, cliff or ruin cuts it or
//    folds it, so it stays one smooth curve over mountains and lies right on flat ground; the HUD (screen space overlay canvases)
//    is drawn after the scene, so the ring never shows through it;
//  - the front: a soft core, gaussian on both sides and softer behind (it trails like a blur), never thinner than about 1.5 px,
//    broken up along the ring by slow noise (brighter and dimmer stretches, never one solid line), with a slight smooth wobble of
//    its radius (smooth noise: never a kink);
//  - behind it a warm glow and radial streaks that ride outward with the front, short streaks ahead of it; streaks thinner than
//    a few pixels fade to their mean (no shimmer when the ring is small or far);
//  - a little noise on the edge: blobs that ride with the front and change slowly;
//  - the mesh's own inner and outer edges never show (a smooth window from _InnerU to _OuterU of the radius);
//  - vertex alpha: the C# fades a direction where the level's ground ends (Level 16's apron), as the old ring stopped there.
// The C# sets _Color and _TrailColor as linear values x the gains (k^1.3 front, k^2.2 trail, as the old ring), _RingCenter,
// _RingRadius (RadiusAt: the same clock as the shove, the deaths and the boom) and _Age every frame. tools/look_model.py of the
// kit is a line by line copy of SoFrag for the previews.
Shader "OpenZ/ShockOverlay"
{
    Properties
    {
        [HDR] _Color ("Front colour (linear, gain from C#)", Color) = (1, 0.82, 0.58, 1)
        [HDR] _TrailColor ("Trail and streak colour (linear, gain from C#)", Color) = (1, 0.34, 0.07, 1)
        _RingCenter ("Blast centre (world xyz)", Vector) = (0, 0, 0, 0)
        _RingRadius ("Front radius (m)", Float) = 10
        _Age ("Wave age (s)", Float) = 0
        _Seed ("Per wave seed", Float) = 0
        _CoreIn ("Core softness behind the front (share of the radius)", Float) = 0.03
        _CoreOut ("Core softness ahead of the front (share of the radius)", Float) = 0.018
        _TrailLen ("Trail fall-off behind the front (share of the radius)", Float) = 0.07
        _GlowLen ("Streak reach ahead of the front (share of the radius)", Float) = 0.025
        _StreakN ("Streak density (about 6.3 x this many round the ring)", Float) = 34
        _StreakAmt ("Streak strength", Range(0, 1)) = 0.85
        _CoreBreak ("Core break-up (0 solid .. 1 fully broken)", Range(0, 1)) = 0.65
        _NoiseAmt ("Edge noise", Range(0, 1)) = 0.45
        _Wobble ("Front wobble (share of the radius)", Float) = 0.012
        _InnerU ("Mesh inner edge (share of the radius)", Float) = 0.62
        _OuterU ("Mesh outer edge (share of the radius)", Float) = 1.1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" }

        Pass
        {
            Name "ShockOverlay"
            Tags { "LightMode"="UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SoVert
            #pragma fragment SoFrag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _TrailColor;
                float4 _RingCenter;
                float _RingRadius;
                float _Age;
                float _Seed;
                float _CoreIn;
                float _CoreOut;
                float _TrailLen;
                float _GlowLen;
                float _StreakN;
                float _StreakAmt;
                float _CoreBreak;
                float _NoiseAmt;
                float _Wobble;
                float _InnerU;
                float _OuterU;
            CBUFFER_END

            struct SoAttributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct SoVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            SoVaryings SoVert(SoAttributes v)
            {
                SoVaryings o = (SoVaryings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.color = v.color;
                return o;
            }

            // value noise, smooth (C1): a front shaped by it never kinks
            float SoHash(float2 p)
            {
                p = frac(p * float2(233.34, 851.73));
                p += dot(p, p + 23.45);
                return frac(p.x * p.y);
            }

            float SoNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = p - i;
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = SoHash(i);
                float b = SoHash(i + float2(1.0, 0.0));
                float c = SoHash(i + float2(0.0, 1.0));
                float d = SoHash(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float4 SoFrag(SoVaryings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 off = i.positionWS.xz - _RingCenter.xz;
                float dist = length(off);
                float2 dir = off / max(dist, 1e-3);
                float R = max(_RingRadius, 0.5);
                float age = _Age;
                float seed = _Seed;
                float px = max(fwidth(dist), 1e-4);                              // metres per pixel across the ring

                // the front, with a slight smooth wobble of its radius
                float wob = SoNoise(dir * 2.2 + float2(seed, age * 0.35)) * 2.0 - 1.0;
                float d = dist - R * (1.0 + _Wobble * wob);                     // metres: + ahead of the front, - behind it

                // the soft core: gaussian, softer behind; broken up along the ring
                float sIn = max(max(_CoreIn * R, 0.8), 1.5 * px);
                float sOut = max(max(_CoreOut * R, 0.55), 1.2 * px);
                float sd = d < 0.0 ? d / sIn : d / sOut;
                float core = exp(-sd * sd);
                float cb = SoNoise(dir * 7.0 + float2(age * 0.9, seed)) * 0.6 + SoNoise(dir * 19.0 + float2(seed * 2.0, -age * 1.4)) * 0.4;
                core *= lerp(1.0 - _CoreBreak, 1.0, smoothstep(0.2, 0.8, cb));

                // radial streaks: noise of the direction only, so they point outward and ride with the front
                float sA = SoNoise(dir * _StreakN + float2(seed * 3.1, age * 1.3));
                float sB = SoNoise(dir * (_StreakN * 0.47) + float2(17.0 - seed, age * 0.7));
                float streak = smoothstep(0.35, 0.95, sA * 0.65 + sB * 0.35);
                float cell = dist / max(_StreakN, 1.0);                          // metres between two streaks
                streak = lerp(0.3, streak, saturate((cell / px - 2.0) / 4.0));

                // the warm glow behind the front (the core's outer edge ahead of it), the streaks long behind and short ahead
                float tau = max(_TrailLen * R, 1.2);
                float trail = d < 0.0 ? exp(d / tau) : exp(-sd * sd);
                float reachIn = tau * lerp(0.5, 1.5, sB);
                float reachOut = max(_GlowLen * R, 0.6) * lerp(0.4, 1.6, sA);
                float rays = streak * (d < 0.0 ? exp(d / reachIn) : exp(-d / reachOut));

                // a little noise on the edge: blobs riding with the front
                float bn = SoNoise(dir * 9.0 + d * 0.22 + float2(seed * 1.7, age * 0.6)) * 0.65
                         + SoNoise(dir * 23.0 + d * 0.5 + float2(-age * 0.8, seed)) * 0.35;
                float breakup = lerp(1.0 - _NoiseAmt, 1.0, smoothstep(0.15, 0.85, bn));

                // the mesh's own edges never show
                float u = dist / R;
                float win = smoothstep(_InnerU, _InnerU + 0.08, u) * (1.0 - smoothstep(_OuterU - 0.03, _OuterU, u));

                float3 rgb = _Color.rgb * core + _TrailColor.rgb * (trail * (1.0 - _StreakAmt * 0.6) + rays * _StreakAmt * 1.2);
                rgb *= breakup * win * i.color.a;

                // fog as OpenZ/FxAdditive: how much of this surface survives it (1 with fog off)
                half fogFactor = InitializeInputDataFog(float4(i.positionWS, 1.0), 0.0);
                half fogVis = MixFog(half3(1.0, 1.0, 1.0), fogFactor).r - MixFog(half3(0.0, 0.0, 0.0), fogFactor).r;
                return float4(rgb * fogVis, 0.0);
            }
            ENDHLSL
        }
    }
}
