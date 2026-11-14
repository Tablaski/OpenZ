// OpenZ/EzDash: EvilZod's power dash look, version 1.3 row 73 (View/OzEvilZodDashLook.cs builds the meshes and sets the values; every number is in
// OzEvilZodDashKnobs). One pass, premultiplied (Blend One OneMinusSrcAlpha): what glows adds light (HDR over the bloom threshold) and also covers a
// share of what is behind it (alpha = _Occlude x its own profile x its fade: by day blue energy reads blue on orange sand instead of lavender; at night
// _Occlude is small and it is almost purely additive); the ground's scorch darkens. No texture property, no keyword of its own (the bend mode reads URP's
// camera opaque texture, as OpenZ/HeatDistort does): nothing a player build can strip.
//  _Mode 0  trail strips (camera-facing, built in C#): uv.x = metres along the trail, uv.y = 0..1 across; uv2.x = HDR gain, uv2.y = 0 soft line .. 1 band
//           with bright edges (the ribbons); vertex alpha = the strip's occlusion x its fade; broken into streaks that run along the trail; a little flicker.
//  _Mode 1  the front half-shield: a shell of a sphere ahead of him, uv.x across the arc, uv.y bottom to top, normals outward: a thin bright rim where the
//           shell turns away from the camera (never under _RimMinPx pixels on screen: then wider and dimmer by as much), brighter near the arc's ends
//           (_Bracket), a faint face and a faint hex pattern gathered toward the rim (_FillToRim: the middle stays clear; _FillMul less at night; the hex
//           fades where a cell is under _HexMinPx pixels), slow energy flow, soft ends; _Break 0..1 is the break: a ripple runs out from the front's
//           centre and the hex cells drop out behind it.
//  _Mode 2  the ground trace (a strip on the ground, built in C#): vertex alpha = scorch alive, uv2.x = glow (HDR); a thin hot line along the middle
//           (_GlowSharp across, _GlowBreak into embers along) that also covers the ground by _Occlude, and a streaky scuffed scorch band. Drawn with a
//           depth offset (_OffF, _OffU) so it never fights the ground.
//  _Mode 3  the afterimages (his baked body, back faces culled: _Cull 2): bright on the silhouette's edge, almost clear in the middle (_Body), with the
//           same occlusion.
//  _Mode 4  the light-bending band (a shell just outside the shield, uv.y 0..1 tapered at the arc's ends, normals outward): the picture behind is pushed
//           outwards along the screen-space normal with a sin(pi v) profile and a few soft waves (_BendCells) along the arc. Drawn at 2985 like the game's
//           heat distortion (before the water, which draws its own surface over it).
// Drawn at the queue the C# gives each material (bend 2985, ground 3001, afterimages 3015, trail 3021, shield 3022).
Shader "OpenZ/EzDash"
{
    Properties
    {
        _Mode ("Mode (0 trail strips, 1 front shield, 2 ground trace, 3 afterimage, 4 bend)", Float) = 0
        _Gain ("Overall gain (from C#: day / night x the knob)", Float) = 1
        [HDR] _Color ("Colour (linear; shield, ground glow, afterimage)", Color) = (0.35, 0.65, 1, 1)
        _ScorchColor ("Scorch colour (ground trace, linear)", Color) = (0.06, 0.05, 0.045, 1)
        _ScorchDark ("Scorch strength", Range(0, 1)) = 1
        _Occlude ("Occlusion where full (from C#: day / night x the part's knob)", Float) = 0.5
        _StreakAmt ("Streak break-up along the trail", Range(0, 1)) = 0.55
        _StreakFreq ("Streaks per metre", Float) = 1.4
        _StreakSpeed ("Streak run (per second)", Float) = 7
        _Flicker ("Energy flicker (0 = calm)", Range(0, 1)) = 0.3
        _Life ("Shield life 0..1", Float) = 1
        _Break ("Shield break 0..1", Float) = 0
        _Rim ("Shield rim", Float) = 2.4
        _RimPow ("Shield rim power", Float) = 3
        _Fill ("Shield face", Float) = 0.08
        _Hex ("Shield hex pattern", Float) = 0.18
        _HexSize ("Hex cell (m)", Float) = 0.34
        _ArcLen ("Shield arc length (m, from C#)", Float) = 6
        _Height ("Shield height (m, from C#)", Float) = 4
        _EdgeSoft ("Shield end softness (share of the arc)", Float) = 0.18
        _RingGain ("Break ripple brightness", Float) = 3.5
        _Seed ("Seed", Float) = 0
        _Alpha ("Afterimage alpha (mode 3) / bend weight (mode 4)", Float) = 1
        _Power ("Afterimage edge power (mode 3)", Float) = 2.5
        _Strength ("Bend push, share of the screen height (mode 4)", Float) = 0.012
        _Bright ("Bend brightening (mode 4)", Float) = 0.05
        _BendCells ("Bend waves along the arc (mode 4)", Float) = 10
        _RimMinPx ("Shield rim, least width on screen (px)", Float) = 1.5
        _FillToRim ("Shield face and hex gathered at the rim (0..1)", Float) = 0.8
        _FillMul ("Shield face and hex, times (from C#: less at night)", Float) = 1
        _HexMinPx ("Shield hex fades under this cell size (px)", Float) = 8
        _Bracket ("Shield rim, extra near the arc's ends", Float) = 1.2
        _BracketW ("Shield rim, share of the arc the bright ends cover", Float) = 0.14
        _GlowSharp ("Ground hot line sharpness across", Float) = 35
        _GlowBreak ("Ground hot line broken into embers (0..1)", Float) = 0.7
        _Body ("Afterimage glow inside the outline (mode 3)", Float) = 0.04
        _OffF ("Depth offset factor", Float) = 0
        _OffU ("Depth offset units", Float) = 0
        _Cull ("Cull (0 off, 2 back: the afterimages)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+21" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "EzDash"
            Tags { "LightMode"="UniversalForward" }

            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]
            Offset [_OffF], [_OffU]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex EzVert
            #pragma fragment EzFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Mode;
                float _Gain;
                float4 _Color;
                float4 _ScorchColor;
                float _ScorchDark;
                float _Occlude;
                float _StreakAmt;
                float _StreakFreq;
                float _StreakSpeed;
                float _Flicker;
                float _Life;
                float _Break;
                float _Rim;
                float _RimPow;
                float _Fill;
                float _Hex;
                float _HexSize;
                float _ArcLen;
                float _Height;
                float _EdgeSoft;
                float _RingGain;
                float _Seed;
                float _Alpha;
                float _Power;
                float _Strength;
                float _Bright;
                float _BendCells;
                float _RimMinPx;
                float _FillToRim;
                float _FillMul;
                float _HexMinPx;
                float _Bracket;
                float _BracketW;
                float _GlowSharp;
                float _GlowBreak;
                float _Body;
                float _OffF;
                float _OffU;
                float _Cull;
            CBUFFER_END

            struct A { float4 pos : POSITION; float3 nrm : NORMAL; float4 col : COLOR; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; };
            struct V
            {
                float4 pos : SV_POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; float3 nrm : TEXCOORD2; float3 view : TEXCOORD3;
                float2 dirSS : TEXCOORD4; float4 grab : TEXCOORD5;
            };

            V EzVert(A i)
            {
                V o;
                float3 w = TransformObjectToWorld(i.pos.xyz);
                o.pos = TransformWorldToHClip(w);
                o.col = i.col; o.uv = i.uv; o.uv2 = i.uv2;
                o.nrm = TransformObjectToWorldNormal(i.nrm);
                o.view = GetWorldSpaceViewDir(w);
                // the bend: the screen-space direction of the outward normal (as OpenZ/HeatDistort's wave mode) and the screen position
                float4 b = TransformWorldToHClip(w + SafeNormalize(o.nrm));                      // the strips have no normals: 0, no NaN
                float2 sa = o.pos.xy / max(abs(o.pos.w), 1e-4), sb = b.xy / max(abs(b.w), 1e-4);
                float2 d = sb - sa;
                #if UNITY_UV_STARTS_AT_TOP
                    d.y = -d.y;
                #endif
                o.dirSS = d / max(length(d), 1e-4);
                o.grab = ComputeScreenPos(o.pos);
                return o;
            }

            float Hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float VNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x), lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), f.x), f.y);
            }
            float2 Mod2(float2 x, float2 y) { return x - y * floor(x / y); }
            // hex tiling (cells of width 1): xy = offset from the cell's centre, z = 0 at the cell's edge .. 0.5 in its middle, w = a number per cell
            float4 Hex(float2 p)
            {
                const float2 r = float2(1.0, 1.7320508);
                float2 h = r * 0.5;
                float2 a = Mod2(p, r) - h, b = Mod2(p - h, r) - h;
                float2 g = dot(a, a) < dot(b, b) ? a : b;
                float2 q = abs(g);
                float d = max(dot(q, normalize(r)), q.x);
                return float4(g, 0.5 - d, Hash21(floor((p - g) * 2.0 + 0.5) + _Seed));
            }

            half4 Strip(V i)
            {
                float a = abs(i.uv.y * 2.0 - 1.0);                                           // 0 in the middle .. 1 at the edge
                float soft = (1.0 - a) * (1.0 - a);
                float rim = smoothstep(0.45, 0.85, a) * (1.0 - smoothstep(0.85, 1.0, a));
                float prof = lerp(soft, 0.35 * soft + rim, saturate(i.uv2.y));
                float s = i.uv.x, t = _Time.y;
                float n = VNoise(float2(s * _StreakFreq - t * _StreakSpeed, i.uv.y * 2.0 + i.uv2.y * 17.0));   // about 2 cells across a strip: smooth at the RTS camera
                float streak = lerp(1.0, 0.3 + 1.4 * n, _StreakAmt);
                float fl = 1.0 + _Flicker * (VNoise(float2(t * 18.0, s * 0.7)) - 0.5);
                float3 c = i.col.rgb * (i.uv2.x * _Gain * prof * streak * fl);
                // what it covers: a soft line covers as it glows; a band (the ribbons) covers its whole width evenly, so its body reads as tinted
                // energy between its bright edges instead of the ground showing through pale
                float cover = lerp(soft, 1.0 - smoothstep(0.8, 1.0, a), saturate(i.uv2.y));
                return half4(c, saturate(_Occlude * i.col.a * cover * saturate(streak)));
            }

            half4 Shield(V i, float2 duv, float fb, float fbw)
            {
                float u = i.uv.x, v = i.uv.y, t = _Time.y;
                // the rim: fb^p; the band where it is over half bright is (1 - 0.5^(1/p)) of fb wide; where that is under _RimMinPx pixels (fbw = fb's change
                // a pixel, taken before the mode branch) a lower power, dimmer by as much (the same light), so the thin rim never shimmers far out
                float p = max(_RimPow, 0.5);
                float lw = min(log2(saturate(1.0 - _RimMinPx * fbw)), -1e-6);
                float pe = clamp(-1.0 / lw, 0.5, p);
                float fres = pow(fb, pe) * (pe + 1.0) / (p + 1.0);
                float toRim = lerp(1.0, sqrt(pow(fb, p)), saturate(_FillToRim)) * _FillMul;    // face and hex gather at the rim: the middle stays clear
                float es = max(_EdgeSoft, 1e-3);
                float win = smoothstep(0.0, es, u) * smoothstep(0.0, es, 1.0 - u) * smoothstep(0.0, 0.16, v) * smoothstep(0.0, 0.12, 1.0 - v);
                float bw = max(_BracketW, 1e-3), ue = min(u, 1.0 - u);                          // the bright ends: just inside the soft ends of the arc
                float bracket = 1.0 + _Bracket * smoothstep(es * 0.5, es * 0.5 + bw * 0.35, ue) * (1.0 - smoothstep(es * 0.5 + bw * 0.65, es * 0.5 + bw, ue));
                float2 m = float2(u * _ArcLen, v * _Height);                                    // metres on the shell
                float4 hc = Hex(m / max(_HexSize, 0.02));
                float aa = max(1.5 * length(duv * float2(_ArcLen, _Height)) / max(_HexSize, 0.02), 1e-3);   // one pixel in cell units (the derivative is taken before the mode branch)
                float cellPx = 1.5 / aa, hexK = smoothstep(0.5 * _HexMinPx, max(_HexMinPx, 1e-3), cellPx);   // cells too small to read fade out (no uniform tint far out)
                float hex = (1.0 - smoothstep(0.045, 0.045 + aa, hc.z)) * hexK;
                float flow = VNoise(float2(u * 9.0 - t * 1.7, v * 5.0 + t * 0.6));
                float fl = 1.0 + _Flicker * 0.5 * (Hash21(float2(floor(t * 24.0), _Seed)) - 0.5);
                // the break: a ripple runs out from the front's centre, the cells drop out behind it, ragged per cell
                float br = saturate(_Break), on = step(1e-3, br);
                float2 q = float2((u - 0.5) * _ArcLen, (v - 0.4) * _Height);
                float d = length(q) / max(0.5 * _ArcLen, 0.5);
                float front = br * 1.35, rw = 0.08 + 0.1 * br;
                float ring = on * exp(-((d - front) * (d - front)) / (rw * rw));
                float keep = lerp(1.0, saturate((d - front + 0.1 + 0.3 * hc.w) / 0.1), on);
                float body = ((_Fill * (0.6 + 0.8 * flow) + hex * _Hex * (0.45 + 0.9 * flow)) * toRim + fres * _Rim * bracket) * keep;
                float lit = win * (body * _Life * (1.0 - br * br) + ring * _RingGain * _Life * (1.0 - br));
                return half4(_Color.rgb * (_Gain * fl * lit), saturate(_Occlude * lit));
            }

            half4 Ground(V i)
            {
                float a = abs(i.uv.y * 2.0 - 1.0);
                float band = 1.0 - smoothstep(0.5, 1.0, a);
                float core = exp(-a * a * max(_GlowSharp, 1.0));                                 // a thin hot line along the middle
                float s = i.uv.x;
                float st = VNoise(float2(s * 0.9, i.uv.y * 3.0)), st2 = VNoise(float2(s * 3.1 + 5.0, i.uv.y * 6.0));   // about 6 cells across: no pixel grain at 35 m
                float scorch = saturate(i.col.a * band * saturate(0.45 + 1.6 * st * st2) * _ScorchDark);
                float glow = i.uv2.x * core * lerp(0.6 + 0.6 * st2, st2 * st2 * 2.0, saturate(_GlowBreak)) * _Gain;   // steady .. broken into embers
                float3 c = _Color.rgb * glow + _ScorchColor.rgb * scorch;
                // the hot line also covers the ground by _Occlude (by day: blue on the sand, not blue added on orange = white / lavender)
                return half4(c, max(scorch, saturate(_Occlude * core * saturate(glow))));
            }

            half4 After(V i)
            {
                float f = 1.0 - saturate(dot(normalize(i.nrm), normalize(i.view)));
                float e = (_Body + pow(max(f, 1e-4), max(_Power, 0.5))) * _Alpha;             // his outline; the body inside almost clear (round 1: a fixed 0.25 = a slab)
                return half4(_Color.rgb * e, saturate(_Occlude * e));
            }

            half4 Bend(V i)
            {
                float prof = sin(saturate(i.uv.y) * 3.14159265);
                float mask = prof * prof * smoothstep(0.0, 0.2, i.uv.x) * smoothstep(0.0, 0.2, 1.0 - i.uv.x) * _Alpha;
                if (mask < 0.002) return half4(0, 0, 0, 0);
                float t = _Time.y;
                float n = VNoise(float2(i.uv.x * _BendCells - t * 0.8, t * 1.3 + i.uv.y * 1.5));
                float2 uv = i.grab.xy / max(i.grab.w, 1e-4);
                float2 suv = uv + i.dirSS * (0.7 + 0.3 * n) * (_Strength * mask) * float2(_ScreenParams.y / _ScreenParams.x, 1.0);
                half3 col = SAMPLE_TEXTURE2D_X_LOD(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, UnityStereoTransformScreenSpaceTex(saturate(suv)), 0).rgb;
                return half4(col * (1.0 + _Bright * mask) * mask, mask);
            }

            half4 EzFrag(V i) : SV_Target
            {
                float2 duv = fwidth(i.uv);
                float fb = saturate(1.0 - abs(dot(SafeNormalize(i.nrm), SafeNormalize(i.view)))), fbw = fwidth(fb);   // the shield's rim term and its change a pixel (before the branch)
                if (_Mode < 0.5) return Strip(i);
                if (_Mode < 1.5) return Shield(i, duv, fb, fbw);
                if (_Mode < 2.5) return Ground(i);
                if (_Mode < 3.5) return After(i);
                return Bend(i);
            }
            ENDHLSL
        }
    }
}
