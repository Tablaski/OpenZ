// OpenZ (GPL-3.0) - version 1.3, level 7 "Pyro Technics": the lava surface, rebuilt to the owner's picture (lava_kits/LV1). A bed of rounded dark lumps with real relief, an orange web
// between them, bright melt pooling at the junctions and in the rivers. Same name and material as item 12's shader; every line of the sum is proved in numpy first
// (LV1/tools/lv1_lib.py frag, the same order and the same numbers), so a value tuned there is a value here. Sheets: LAV_Lava and LAV_Border_* (View/OzLava.Surface.cs).
//   maps     _LumpMap  T_Lava_Lumps  r height, g share of the surface nearer the vein (opening the web by x uncovers x of the surface), b per-lump random, a fine detail
//            _LumpNrm  T_Lava_LumpsN r, g normal, b smooth blotches (no texel grid at any size: they set the flow phase and the heat)
//            _FieldMap T_L07_Field   r, g flow, b distance to the visible shore (the ground mesh crossing the sheet), nearer in a strip, a rivers. World rect _FieldRect. Clamped, no mips.
//   vertex   r 0 at the rim .. 1 inside, g hot pool (Z's hot tiles), b field id (id + 0.5) / 16, a 1 = reads the wake
//   sum      T = (veins + rivers + hot) * heat * wave + surge + wake (LV_CONTRACT.md 5.1). Heat 1, no wave, no surge is the picture.
//   globals  all written by the lava kits, all with a safe value from OzLavaIds.Neutral. _OZLavaTime stops with the pause. _OZLavaLite 1 = LIGHTEST: one phase, plain slow scroll.
// 7 texture reads plus the shadow, 8 with a wake. No keyword of its own. Colours only through Shaders/OzLavaRamp.hlsl.
Shader "OpenZ/Lava"
{
    Properties
    {
        [NoScaleOffset] _LumpMap ("Lumps (height, vein, random, fine)", 2D) = "gray" {}
        [NoScaleOffset] _LumpNrm ("Lump normal (x, y, blotches)", 2D) = "gray" {}
        [NoScaleOffset] _FieldMap ("L07 field (flow, bank, rivers)", 2D) = "black" {}
        _FieldRect ("Field rect: min x, min z, 1 / size x, 1 / size z", Vector) = (-24, -244, 0.0046296, 0.0037313)
        _Tile ("Lump tile, m (16 main lumps across)", Float) = 16.5
        _CycleS ("Flow cycle, s", Float) = 150
        _FlowSpeed ("Flow speed, m/s", Float) = 0.006
        _PhaseTile ("Phase blotch tile, m", Float) = 41
        _HeatTile ("Heat blotch tile, m", Float) = 22
        _OBase ("Opening: base", Float) = 0.36
        _ORiver ("Opening: rivers", Float) = 0.2
        _OHot ("Opening: hot tiles", Float) = 0.06
        _ONoise ("Opening: blotches", Float) = 2.4
        _OBank0 ("Bank: crust to", Float) = 0.04
        _OBank1 ("Bank: open from", Float) = 0.55
        _OEdge ("Bank: extra crust", Float) = 0.40
        _OLump ("Opening: per lump", Float) = 0.34
        _V0 ("Web: least", Float) = 0.03
        _V1 ("Web: by opening", Float) = 0.28
        _VMax ("Web: most", Float) = 0.34
        _DFine ("Web edge roughness", Float) = 0.13
        _TLo ("T: vein", Float) = 0.7
        _THi ("T: hottest", Float) = 2
        _OT0 ("Hot from opening", Float) = 0.4
        _OT1 ("Hot to opening", Float) = 0.75
        _G0 ("Hot in the gap from", Float) = 0
        _G1 ("Hot in the gap to", Float) = 0.17
        _GOp ("Gap by opening", Float) = 0.04
        _RimW ("Soft edge width", Float) = 0.2
        _RimT ("Soft edge T", Float) = 0.5
        _Skin ("Skins on the melt", Float) = 0.65
        _TF0 ("T by fine: base", Float) = 0.62
        _TF1 ("T by fine: gain", Float) = 0.8
        _Sink0 ("Sunk lump: by random", Float) = 0.5
        _Sink1 ("Sunk lump: from", Float) = 1.15
        _SinkW ("Sunk lump: width", Float) = 0.14
        _TSink ("Sunk lump: T", Float) = 0.74
        _NormalK ("Relief", Float) = 2.8
        _SunGain ("Sun on the crust", Float) = 0.60
        _SunTint ("Takes the warm day sun's colour out of the crust (the sun is DayNight's: intensity about 1.9, colour 1 0.8 0.53 linear), as the picture's colours are final", Vector) = (1, 1.25, 1.85, 0)
        _Amb ("Ambient on the crust", Float) = 0.34
        _Ao ("Crevice dark", Float) = 2.6
        _Lit ("Lit tops", Float) = 1.0
        _CrustGain ("Crust gain (set against captures)", Float) = 1.7
        _AlbDark ("Lump dark", Color) = (0.2, 0.12, 0.12, 1)
        _AlbMid ("Lump mid", Color) = (0.3, 0.22, 0.22, 1)
        _AlbLit ("Lump lit top", Color) = (0.4824, 0.3412, 0.2784, 1)
        _SurgeGain ("Surge bump", Float) = 0.2
        _FineTile ("Small stones tile, m", Float) = 8.3
        _FineMix ("Small stones in the height", Float) = 0.45
        _FineN ("Small stones in the normal", Float) = 0.8
        _V2A ("Small web: by opening", Float) = 0.012
        _V2B ("Small web: opens from", Float) = 0.25
        _Facet ("Plate facets: tilt read", Float) = 1.4
        _FacetK ("Plate facets: light across a plate", Float) = 0.75
        _HeatPow ("Emission by heat (power)", Float) = 1.4
        _HeatT ("Heat share in the colour", Float) = 0.45
        _WakeGain ("Wake bump", Float) = 0.7
        _ShoreWidth ("Shore: the one number to tune: every width of the shore, 0.5 thin .. 1.6 wide", Float) = 0.8
        _ShoreReach ("Shore: how far the bays wander in, field units", Float) = 0.12
        _ShoreBay0 ("Shore: mean bay", Float) = 0.42
        _ShoreBay1 ("Shore: bays by the heat blotches", Float) = 2.2
        _ShoreBay2 ("Shore: bays by the phase blotches", Float) = 1.4
        _ShorePlate ("Shore: tongues, how far a big plate reaches in", Float) = 0.26
        _ShoreS0 ("Shore: stone edge from", Float) = 0.07
        _ShoreS1 ("Shore: stone edge to", Float) = 0.09
        _ShoreDark ("Shore: crust darkens to", Float) = 0.78
        _ShoreStone ("Shore: stone colour where the plates join the crust", Color) = (0.2431, 0.149, 0.1294, 1)
        _ShoreStone2 ("Shore: stone colour at the cut, the ground rim", Color) = (0.4392, 0.2275, 0.1569, 1)
        _ShoreStoneW ("Shore: how far in the second colour holds", Float) = 0.12
        _ShoreTone0 ("Shore: stone tone base", Float) = 0.6
        _ShoreTone1 ("Shore: stone tone gain", Float) = 0.9
        _ShoreAo ("Shore: stone crevice power", Float) = 0.5
        _ShoreRelief ("Shore: stone facets", Float) = 0.5
        _ShoreT ("Shore: heat under the rim, the ground paint at the cut", Float) = 0.42
        _ShoreG0 ("Shore: heat ends from", Float) = 0.08
        _ShoreG1 ("Shore: heat ends to", Float) = 0.16
        _ShoreTop ("Shore: heat on the stone tops", Float) = 0.85
        _ShoreLine ("Shore: full heat out to", Float) = 0.14
        _ShoreWarm ("Shore: night, the light of the lava lights on the stones", Vector) = (3.5, 0.55, 0.45, 0)
        _ShoreMood ("Shore: breath, as the ground", Float) = 2.2
        _ShoreNight ("Shore: night heat, as the ground", Float) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _FieldRect, _AlbDark, _AlbMid, _AlbLit, _SunTint, _ShoreStone, _ShoreStone2, _ShoreWarm;
            float _Tile, _CycleS, _FlowSpeed, _PhaseTile, _HeatTile, _OBase, _ORiver, _OHot, _ONoise, _OBank0, _OBank1, _OEdge, _OLump, _V0, _V1, _VMax, _DFine, _TLo, _THi,
                  _OT0, _OT1, _G0, _G1, _GOp, _RimW, _RimT, _Skin, _TF0, _TF1, _Sink0, _Sink1, _SinkW, _TSink, _NormalK, _SunGain, _Amb, _Ao, _Lit, _CrustGain, _SurgeGain, _WakeGain, _Facet, _FacetK, _HeatPow, _HeatT, _FineTile, _FineMix, _FineN, _V2A, _V2B,
                  _ShoreWidth, _ShoreReach, _ShoreBay0, _ShoreBay1, _ShoreBay2, _ShorePlate, _ShoreS0, _ShoreS1, _ShoreDark, _ShoreStoneW, _ShoreTone0, _ShoreTone1, _ShoreAo, _ShoreRelief, _ShoreT, _ShoreG0, _ShoreG1, _ShoreTop, _ShoreLine, _ShoreMood, _ShoreNight;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On ZTest LEqual Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "OzLavaRamp.hlsl"

            TEXTURE2D(_LumpMap); SAMPLER(sampler_LumpMap);
            TEXTURE2D(_LumpNrm); SAMPLER(sampler_LumpNrm);
            TEXTURE2D(_FieldMap); SAMPLER(sampler_FieldMap);
            TEXTURE2D(_OZLavaWake); SAMPLER(sampler_OZLavaWake);
            float4 _OZLavaField[16];
            float4 _OZLavaWave, _OZLavaSurge, _OZLavaFlash, _OZLavaWakeRect;
            float _OZLavaTime, _OZLavaLite, _OZLavaMood;
            half _OZNight;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half4 color : TEXCOORD1; half fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(v);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.color = v.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _OZLavaTime;
                float2 w = i.positionWS.xz;
                int id = clamp((int)(i.color.b * 16.0), 0, 15);
                float4 fv = _OZLavaField[id];
                float4 fm = SAMPLE_TEXTURE2D(_FieldMap, sampler_FieldMap, (w - _FieldRect.xy) * _FieldRect.zw);
                float2 flow = fm.rg * 2.0 - 1.0;
                float bank = fm.b / max(_ShoreWidth, 0.05), river = fm.a;

                // ---- the two lump layers. Phase offset from smooth world blotches, so no moment is a full-lake change; the higher lump wins, never a mix of two patterns
                float slow = SAMPLE_TEXTURE2D_LOD(_LumpNrm, sampler_LumpNrm, w / _PhaseTile, 3.0).b;
                float4 s; float2 nxy, uF;
                if (_OZLavaLite > 0.5)
                {
                    float2 uv = (w + float2(0.012, -0.008) * t) / _Tile;                                   // LIGHTEST: one phase, a plain slow scroll, no flow map
                    s = SAMPLE_TEXTURE2D(_LumpMap, sampler_LumpMap, uv);
                    nxy = SAMPLE_TEXTURE2D(_LumpNrm, sampler_LumpNrm, uv).rg;
                    uF = uv;
                }
                else
                {
                    float ph = t / _CycleS + slow * 2.0;
                    float p0 = frac(ph), p1 = frac(ph + 0.5), ad = _CycleS * _FlowSpeed;
                    float2 uA = (w - flow * (p0 - 0.5) * ad) / _Tile + floor(ph) * float2(0.137, 0.271);                    // a new place in the map each cycle, changed while the layer is hidden
                    float2 uB = (w - flow * (p1 - 0.5) * ad) / _Tile + float2(0.37, 0.61) + floor(ph + 0.5) * float2(0.173, 0.229);
                    float4 a = SAMPLE_TEXTURE2D(_LumpMap, sampler_LumpMap, uA), b = SAMPLE_TEXTURE2D(_LumpMap, sampler_LumpMap, uB);
                    float2 na = SAMPLE_TEXTURE2D(_LumpNrm, sampler_LumpNrm, uA).rg, nb = SAMPLE_TEXTURE2D(_LumpNrm, sampler_LumpNrm, uB).rg;
                    float wa = 1.0 - abs(2.0 * p0 - 1.0);
                    float m = smoothstep(-0.08, 0.08, a.r - b.r + (wa - 0.5) * 3.2);
                    s = lerp(b, a, m); nxy = lerp(nb, na, m);
                    uF = m > 0.5 ? uA : uB;
                }
                // ---- small stones on the plates and the small web inside the hot zones: one more map read at a third of the size, same creep (explicit gradients: uF jumps where the layers change)
                uF *= _Tile / _FineTile;
                float2 gx = ddx(w) / _FineTile, gy = ddy(w) / _FineTile;
                float4 s2 = SAMPLE_TEXTURE2D_GRAD(_LumpMap, sampler_LumpMap, uF, gx, gy);
                nxy += (SAMPLE_TEXTURE2D_GRAD(_LumpNrm, sampler_LumpNrm, uF, gx, gy).rg - 0.5) * _FineN;
                float h = s.r * (1.0 - _FineMix + _FineMix * 1.5 * s2.r), vd = s.g, rnd = s.b, fine = s.a;

                // ---- how open the crust is here
                float hb = SAMPLE_TEXTURE2D_LOD(_LumpNrm, sampler_LumpNrm, w / _HeatTile + float2(0.13, 0.29), 3.0).b;
                float se = bank - _ShoreReach * saturate(_ShoreBay0 - _ShoreBay1 * (hb - 0.5) + _ShoreBay2 * (slow - 0.5));   // the shore as the crust feels it: bays from world blotches, they stand still
                float bc = smoothstep(_OBank0, _OBank1, se);
                float op = (_OBase + _ORiver * river + _OHot * i.color.g + _ONoise * (hb - 0.5)) * bc - _OEdge * (1.0 - bc) - fv.y;
                float wake = 0.0;
                if (_OZLavaWakeRect.z > 0.0 && i.color.a > 0.5)
                {
                    wake = SAMPLE_TEXTURE2D_LOD(_OZLavaWake, sampler_OZLavaWake, (w - _OZLavaWakeRect.xy) * _OZLavaWakeRect.zw, 0.0).r;
                    op += wake * 0.5;
                }
                float opl = op + _OLump * (rnd - 0.5);
                float dist = vd + (fine - 0.5) * _DFine;
                float wv = clamp(_V0 + _V1 * opl, 0.0, _VMax);
                float warm = smoothstep(-0.02, 0.14, opl);
                float gap = max(wv - dist, clamp(_V2A * (opl - _V2B), 0.0, _VMax) - (s2.g + (s2.a - 0.5) * _DFine));

                // ---- temperature: the one curve from the lump's rim through the vein to the melt
                float Tc = _TLo + (_THi - _TLo) * smoothstep(_OT0, _OT1, op + (fine - 0.5) * 0.35);
                float Tw = _TLo + (Tc - _TLo) * smoothstep(_G0, _G1, gap + _GOp * (op - 0.5));
                float Te = _RimT + (_TLo - _RimT) * smoothstep(-_RimW, 0.0, gap);
                float skin = 1.0 - _Skin * smoothstep(0.55, 0.72, fine) * smoothstep(0.0, 0.2, gap);
                float T = (gap > 0.0 ? Tw : Te) * warm * smoothstep(-_RimW * 1.7, -_RimW * 0.5, gap) * (_TF0 + _TF1 * fine) * skin;
                float th = rnd * _Sink0 + _Sink1;
                T = max(T, smoothstep(th, th + _SinkW, op) * (0.72 + 0.28 * (1.0 - h)) * _TSink);

                float wave = 1.0 + _OZLavaWave.x * sin(_OZLavaWave.y * w.x + _OZLavaWave.z * w.y + _OZLavaWave.w);
                T *= (1.0 + (fv.x - 1.0) * _HeatT) * wave;
                if (_OZLavaSurge.w > 0.0 && fv.z > 0.0)
                {
                    float d = (dot(w, _OZLavaSurge.xy) - _OZLavaSurge.z) / _OZLavaSurge.w;
                    T += _SurgeGain * fv.z * exp(-d * d) * smoothstep(0.2, 0.9, T + 0.3);
                }
                T += wake * _WakeGain;

                // ---- crust: lit from above, dark in the crevices. The sheet looks up, so the map's axes are the world's; the image's rows run against world z
                float nx = (nxy.x * 2.0 - 1.0) * _NormalK, nz = -(nxy.y * 2.0 - 1.0) * _NormalK;
                float ny = sqrt(max(1.0 - nx * nx - nz * nz, 0.02));
                float4 sc = TransformWorldToShadowCoord(i.positionWS);
                Light L = GetMainLight(sc);
                float ndl = saturate(nx * L.direction.x + ny * L.direction.y + nz * L.direction.z);
                float tone = saturate(h * 0.8 + (rnd - 0.5) * 0.75 + (fine - 0.5) * 0.5);
                float tl = _Lit * tone * h * 0.55;
                float3 alb = lerp(lerp(_AlbDark.rgb, _AlbMid.rgb, tone), _AlbLit.rgb, tl);
                float ao = pow(min(0.10 + 1.05 * saturate(h * 1.5), 1.15), _Ao);
                float3 sun = L.color * lerp(_SunTint.rgb, float3(1.0, 1.0, 1.0), _OZNight);
                float facet = saturate(0.5 + _Facet * (nx * 0.55 + nz * 0.83));                            // a low light across each plate, so it reads as a faceted stone from straight above too
                float3 crust = alb * (_Amb * (1.0 - 0.5 * _OZNight) + _SunGain * sun * ndl * L.shadowAttenuation) * ao * _CrustGain * lerp(1.0 - _FacetK, 1.0 + _FacetK, facet) * lerp(_ShoreDark, 1.0, bc);
                if (_OZLavaFlash.w > 0.0)
                {
                    float f = saturate(1.0 - distance(w, _OZLavaFlash.xy) / max(_OZLavaFlash.z, 0.01));
                    crust += alb * float3(1.0, 0.45, 0.15) * _OZLavaFlash.w * f * f;
                }

                float glow = OzLavaGlow((half)T);
                float3 c = crust * (1.0 - glow) + OzLavaEmit((half)T, _OZNight) * pow(max(fv.x, 0.01), _HeatPow);    // the breath must read at the game camera: the light itself swells with the heat
                // ---- the rim: near the shore the plates join the ground's stones, lit as the ground is and with the ground's heat, so the cut of the sheet against the mesh has one colour on both sides and the eye follows the plates' edge
                float tongue = rnd * rnd * rnd;
                float rimE = se - _ShorePlate * tongue;                                                    // a few big plates reach in as tongues, most stay near the rim
                if (rimE < max(_ShoreS1, _ShoreG1))                                                        // only the rim pays for this block
                {
                    float stone = 1.0 - smoothstep(_ShoreS0, _ShoreS1, rimE);
                    float rimT = _ShoreT * (1.0 - smoothstep(_ShoreG0, _ShoreG1, rimE)) * lerp(_ShoreTop, 1.0, max(1.0 - bank / _ShoreLine, 1.0 - saturate(h * 2.0)));   // the heat stays under the rim, in the seams
                    float3 groundLit = SampleSH(float3(0.0, 1.0, 0.0)) + L.color * saturate(L.direction.y) * L.shadowAttenuation + _ShoreWarm.rgb * _OZNight;
                    float3 stoneAlb = lerp(_ShoreStone2.rgb, _ShoreStone.rgb, smoothstep(0.0, _ShoreStoneW, se + 0.5 * _ShoreReach + _ShorePlate * 0.3 * (0.5 - tongue)));
                    float3 stoneC = stoneAlb * (_ShoreTone0 + _ShoreTone1 * tone) * groundLit * pow(max(ao, 0.0001), _ShoreAo) * (1.0 + _ShoreRelief * (2.0 * facet - 1.0));
                    float mood = _OZLavaMood > 0.01 ? _OZLavaMood : 1.0;
                    c = lerp(c, stoneC, stone) + OzLavaEmit((half)rimT, _OZNight) * pow(mood, _ShoreMood) * (1.0 + _ShoreNight * _OZNight);
                }
                c = MixFog(c, i.fogFactor);
                return half4(c, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes v) { Varyings o; UNITY_SETUP_INSTANCE_ID(v); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings vert(Attributes v) { Varyings o; UNITY_SETUP_INSTANCE_ID(v); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(v.normalOS); return o; }
            half4 frag(Varyings i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
