// OpenZ/UI/PosterFigure: the faction screen's 3D poster characters in the UI (1.2 owner items 88 / 89; UI/OpenZMainMenu.Faction.cs,
// View/OzFactionPosters.cs). _MainTex is the poster camera's render (a RenderTexture cleared to transparent black): its colour is
// already multiplied by its own coverage at the anti-aliased edges, so it is drawn with premultiplied blending (One, OneMinusSrcAlpha)
// and the edges keep their own colour instead of a dark fringe. A thin paper rim (_RimColor, _RimPx render texels wide) is drawn round
// the characters, as the white print outline of the owner's poster pictures (0 = none). The UI colour tints the picture and its alpha
// fades it (premultiplied). The uGUI stencil block keeps it usable under a Mask.
// k75a2, the print mode (_Print 1): the poster's print layers (straight alpha) drawn with the same blend; their light paper (light and
// nearly colourless, judged in sRGB steps: _PaperLum, _PaperChroma) is aged (_PaperTone) and takes _PaperSee of the screen's colour
// behind the poster (_PaperBehind, set by the menu each frame), so it no longer reads as flat bright white; the ink, the colours and
// the alpha stay. The render's paper rim is aged the same way. With _PaperSee 0 and a white _PaperTone everything is as before.
// k75a3, the live print (_Live above 0, only while its poster is in focus): the layer's picture moves inside the weights of its poster's
// motion map (_MotionMap: 0 = never moves): it turns (_Spin, a fan with _SpinMirror), breathes (_Breathe), waves like hanging cloth
// (_Wave) or drifts (_Drift), all scaled by _Live; _Glow lights the weighted pixels (the robot army's eyes); a soft light band
// (_Sweep) passes over the paper. With _Live 0 the print draws exactly as before. The VS sign (_Print 2): a premultiplied picture
// (additive light where its alpha is 0) with the light band (_Sweep) passing through it, masked by its own coverage.
// k75a4, the 3D render (_Print 0): _FxMode 1 = a slanted light band (_Glint, _GlintSpan) across the two spots (_SpotC, _SpotM1 / 2:
// Zod's lenses), only on their dark glass; _FxMode 2 = the two spots' own red-orange light brighter and a soft halo round them
// (_EyeGlow, _EyeTone: EvilZod's eyes); _Fill (its last value above 0) completes the render downward in a strip (EvilZod's cape down
// to the frame band: its own row at the strip's top laid under what is there, the paper rim following it). All 0: as before.
// k83, the VS sign's halo (_Print 3): a soft glow made in sRGB steps (straight alpha); its colour is mixed with the screen's colour
// behind it (_PaperBehind) in sRGB steps and handed to the premultiplied blend, so over that colour it reads as drawn (blended in
// linear light the glow read two to three times as strong: a smeared red / blue cloud round the sign).
Shader "OpenZ/UI/PosterFigure"
{
    Properties
    {
        [PerRendererData] _MainTex ("Poster camera render", 2D) = "black" {}
        _RimColor ("Paper rim colour", Color) = (0.96, 0.94, 0.88, 1)
        _RimPx ("Paper rim width (render texels, 0 = none)", Float) = 2
        _RimAlpha ("Paper rim strength", Range(0, 1)) = 1
        _Print ("Print layer (1: a straight-alpha print layer, 0: the 3D render)", Float) = 0
        _PaperSee ("Paper: share of the colour behind the poster", Range(0, 1)) = 0
        _PaperTone ("Paper: aged tone (multiplies the light paper)", Color) = (1, 1, 1, 1)
        _PaperBehind ("Paper: the screen's colour behind the poster", Color) = (0.19, 0.13, 0.09, 1)
        _PaperLum ("Paper: lightness from / to (sRGB 0..1)", Vector) = (0.55, 0.8, 0, 0)
        _PaperChroma ("Paper: colourfulness from / to (sRGB 0..1)", Vector) = (0.1, 0.28, 0, 0)
        _PaperSeeInk ("Paper: share of the mid layer's white print", Range(0, 1)) = 0
        [NoScaleOffset] _PaperInk ("Paper: the mid layer's picture (its white is print)", 2D) = "black" {}
        _Live ("Live print: weight (0 = still)", Range(0, 1)) = 0
        _LiveT ("Live print: clock (s)", Float) = 0
        _MotionMap ("Live print: motion map (r g b a weights)", 2D) = "black" {}
        _LayerPx ("Live print: the layer's pixels (x, y)", Vector) = (1152, 2048, 0, 0)
        _Spin ("Live print: turn (middle uv, deg, period s)", Vector) = (0.5, 0.5, 0, 0)
        _SpinW ("Live print: turn weights", Vector) = (0, 0, 0, 0)
        _SpinMirror ("Live print: turn mirrored (a fan)", Float) = 0
        _Breathe ("Live print: breathe (middle uv, scale, period s)", Vector) = (0.5, 0.5, 0, 0)
        _BreatheW ("Live print: breathe weights", Vector) = (0, 0, 0, 0)
        _Wave ("Live print: wave (px, wavelength px, waves per s, phase spread)", Vector) = (0, 0, 0, 0)
        _WaveW ("Live print: wave weights", Vector) = (0, 0, 0, 0)
        _Drift ("Live print: drift (px x, px y, period s, phase spread)", Vector) = (0, 0, 0, 0)
        _DriftW ("Live print: drift weights", Vector) = (0, 0, 0, 0)
        _Glow ("Live print: glow (amount, period s, phase spread)", Vector) = (0, 0, 0, 0)
        _GlowW ("Live print: glow weights", Vector) = (0, 0, 0, 0)
        _Sweep ("Light band (place, width, strength, ink share)", Vector) = (-1, 0.1, 0, 0.33)
        _SweepTone ("Light band colour", Color) = (1, 0.96, 0.86, 1)
        _FxMode ("Render effect (0 none, 1 glint, 2 glow)", Float) = 0
        _SpotC ("Effect spots: middles (uv x1 y1 x2 y2)", Vector) = (0, 0, 0, 0)
        _SpotM1 ("Effect spot 1: uv to its ellipse (rows xy, zw)", Vector) = (0, 0, 0, 0)
        _SpotM2 ("Effect spot 2: uv to its ellipse (rows xy, zw)", Vector) = (0, 0, 0, 0)
        _Glint ("Glint band (place, width, strength, slant)", Vector) = (-9, 0.05, 0, 0.55)
        _GlintSpan ("Glint band from / to (render px), the glass's darkness from / to", Vector) = (0, 1, 0.04, 0.08)
        _EyeGlow ("Eye glow (amount, own light, halo, halo size px)", Vector) = (0, 0, 0, 1)
        _EyeTone ("Eye glow colour", Color) = (1, 0.26, 0.07, 1)
        _Fill ("Fill down (from v, source v, right u, soft u; 0 = off)", Vector) = (0, 0, 0, 0)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            sampler2D _MainTex; float4 _MainTex_TexelSize; float4 _MainTex_ST;
            fixed4 _RimColor; float _RimPx, _RimAlpha;
            float _Print, _PaperSee, _PaperSeeInk; fixed4 _PaperTone, _PaperBehind; float4 _PaperLum, _PaperChroma; sampler2D _PaperInk;   // k105: + the ink share
            sampler2D _MotionMap; float _Live, _LiveT, _SpinMirror; float4 _LayerPx, _Spin, _SpinW, _Breathe, _BreatheW, _Wave, _WaveW, _Drift, _DriftW, _Glow, _GlowW, _Sweep; fixed4 _SweepTone;   // k75a3
            float _FxMode; float4 _SpotC, _SpotM1, _SpotM2, _Glint, _GlintSpan, _EyeGlow, _Fill; fixed4 _EyeTone;   // k75a4

            // k75a2: how much a texel (linear colour) is light paper: 0 = ink / colour, 1 = paper (light and nearly colourless, in sRGB steps)
            float PaperOf(float3 lin)
            {
                float3 g = lin;
            #ifndef UNITY_COLORSPACE_GAMMA
                g = LinearToGammaSpace(lin);
            #endif
                float l = dot(g, float3(0.299, 0.587, 0.114));
                float ch = max(g.r, max(g.g, g.b)) - min(g.r, min(g.g, g.b));
                return smoothstep(_PaperLum.x, _PaperLum.y, l) * (1 - smoothstep(_PaperChroma.x, _PaperChroma.y, ch));
            }

            // k75a2: aged paper: toned, and blended toward the screen's colour behind the poster (linear light), by the share see (k105)
            float3 Aged(float3 lin, float see)
            {
                return lerp(lin * _PaperTone.rgb, _PaperBehind.rgb, see);
            }

            // k105: the share a print texel's paper takes: _PaperSee; on the mid layer (its picture is _PaperInk: the same texel at the same
            // place) its white print keeps _PaperSeeInk
            float SeeOf(float2 uv, float4 c)
            {
                float4 m = tex2D(_PaperInk, uv);
                float mid = step(dot(abs(c - m), float4(1, 1, 1, 1)), 0.004);
                return lerp(_PaperSee, _PaperSeeInk, mid);
            }

            // k75a3: where the live print samples its picture (in the layer's pixels: turn, breathe, wave, drift, each by its weights)
            float2 LiveUV(float2 uv)
            {
                float4 m = tex2D(_MotionMap, uv);
                float2 L = max(_LayerPx.xy, float2(1, 1));
                float2 p = uv * L;
                float tau = 6.2831853;
                float ws = dot(m, _SpinW) * _Live;
                if (_Spin.w > 0.001 && ws > 0.0001)
                {
                    float2 c = _Spin.xy * L; float2 r = p - c;
                    float side = _SpinMirror > 0.5 ? clamp(r.x / 40.0, -1.0, 1.0) : 1.0;
                    float a = -radians(_Spin.z) * sin(tau * _LiveT / _Spin.w) * side * ws;                // the picture turns by -a: sample from the turn back
                    float sa = sin(a), ca = cos(a);
                    p = c + float2(ca * r.x - sa * r.y, sa * r.x + ca * r.y);
                }
                float wb = dot(m, _BreatheW) * _Live;
                if (_Breathe.w > 0.001 && wb > 0.0001)
                {
                    float2 c = _Breathe.xy * L;
                    float k = 1.0 + _Breathe.z * (0.5 - 0.5 * cos(tau * _LiveT / _Breathe.w)) * wb;
                    p = c + (p - c) / k;
                }
                float ww = dot(m, _WaveW) * _Live;
                if (_Wave.y > 0.001 && ww > 0.0001) p.x -= _Wave.x * ww * sin(tau * ((L.y - p.y) / _Wave.y - _Wave.z * _LiveT) + _Wave.w * uv.x);
                float wd = dot(m, _DriftW) * _Live;
                if (_Drift.z > 0.001 && wd > 0.0001)
                    p -= float2(_Drift.x * sin(tau * _LiveT / _Drift.z + _Drift.w * uv.x), _Drift.y * sin(tau * _LiveT / (_Drift.z * 1.37) + 1.3 + _Drift.w * uv.x)) * wd;
                return p / L;
            }

            // k75a3: the soft light band across the picture's diagonal (0 at its top left, 1 at its bottom right); none at place -1
            float SweepBand(float2 uv)
            {
                if (_Sweep.x < -0.5) return 0;
                float asp = _LayerPx.x / max(_LayerPx.y, 1.0);
                float s = (uv.x * asp + (1.0 - uv.y)) / (asp + 1.0);
                float d = (s - _Sweep.x) / max(_Sweep.y, 0.001);
                return exp(-d * d);
            }

            // k75a3: the live print's light: the weighted pixels' glow (the army's eyes) and the band over the paper (the ink a share)
            float3 LiveLight(float2 uvm, float2 uv0, float paper)
            {
                float4 m = tex2D(_MotionMap, uvm);
                float3 add = _SweepTone.rgb * SweepBand(uv0) * _Sweep.z * lerp(_Sweep.w, 1.0, paper);
                float g = dot(m, _GlowW);
                if (_Glow.y > 0.001 && g > 0.0001)
                    add += float3(1.0, 0.45, 0.3) * g * _Glow.x * _Live * (0.5 + 0.5 * sin(6.2831853 * _LiveT / _Glow.y + uv0.x * _Glow.z));
                return add;
            }

            // k75a4: the render completed downward in the fill strip (its own row at the strip's top laid under what is there; the row's
            // colour from mip 3, an 8 x 8 texel average, so no thin highlight is drawn out into streaks; its coverage at least 1.6 x that
            // average's, so a sliver of paper between the cape and the armour closes: OzFactionPolish.FillMip / FillGrow)
            float4 Filled(float2 uv, float4 c)
            {
                float4 s = tex2D(_MainTex, float2(uv.x, _Fill.y));
                float4 b = tex2Dlod(_MainTex, float4(uv.x, _Fill.y, 0, 3));
                float sa = saturate(max(s.a, b.a * 1.6));
                s = float4(b.rgb / max(b.a, 0.001) * sa, sa);
                float m = step(_Fill.x, uv.y) * step(uv.y, _Fill.y) * (1 - smoothstep(_Fill.z - _Fill.w, _Fill.z, uv.x));
                return c + s * (1 - c.a) * m;
            }

            // k75a4: the render's coverage at uv (filled when the fill is on): the paper rim follows it
            float FigA(float2 uv)
            {
                float4 c = tex2D(_MainTex, uv);
                if (_Fill.w > 0) c = Filled(uv, c);
                return c.a;
            }

            // k75a4: how far uv is inside a spot's ellipse (1 on its edge)
            float SpotR(float2 uv, float2 mid, float4 m)
            {
                float2 d = uv - mid;
                return length(float2(dot(m.xy, d), dot(m.zw, d)));
            }

            // k75a4: the render's added light (premultiplied): the glint across the lenses' dark glass, or the eyes' own light and halo
            float3 FigureFx(float2 uv, float4 c)
            {
                if (_FxMode < 0.5) return float3(0, 0, 0);
                float r1 = SpotR(uv, _SpotC.xy, _SpotM1), r2 = SpotR(uv, _SpotC.zw, _SpotM2);
                float3 lin = c.rgb / max(c.a, 0.001);
                float2 px = uv * _MainTex_TexelSize.zw;
                if (_FxMode < 1.5)
                {
                    if (_Glint.x < -1.0) return float3(0, 0, 0);
                    float lens = max(1 - smoothstep(0.88, 1.0, r1), 1 - smoothstep(0.88, 1.0, r2));
                    float dark = 1 - smoothstep(_GlintSpan.z, _GlintSpan.w, dot(lin, float3(0.2126, 0.7152, 0.0722)));
                    float q = (px.x - _Glint.w * px.y - _GlintSpan.x) / max(_GlintSpan.y - _GlintSpan.x, 1.0);
                    float b = (q - _Glint.x) / max(_Glint.y, 0.001);
                    float band = exp(-b * b) * 0.55 + exp(-b * b * 9.0) * 0.45;
                    return _SweepTone.rgb * (_Glint.z * band * lens * dark * c.a);
                }
                float core = max(1 - smoothstep(0.75, 1.3, r1), 1 - smoothstep(0.75, 1.3, r2));
                float red = saturate((lin.r - 1.5 * lin.g - lin.b) * 3.0);
                float2 e1 = (uv - _SpotC.xy) * _MainTex_TexelSize.zw / max(_EyeGlow.w, 1.0);
                float2 e2 = (uv - _SpotC.zw) * _MainTex_TexelSize.zw / max(_EyeGlow.w, 1.0);
                float halo = exp(-dot(e1, e1) * 2.2) + exp(-dot(e2, e2) * 2.2);
                return c.rgb * (_EyeGlow.y * _EyeGlow.x * core * red) + _EyeTone.rgb * (_EyeGlow.z * _EyeGlow.x * halo);
            }

            // k83: the VS sign's halo (_Print 3): its colour mixed with the screen's colour behind it in sRGB steps, premultiplied
            float4 HaloMix(float4 c, float4 tint)
            {
                float a = saturate(c.a * tint.a);
                float3 col = c.rgb * tint.rgb;
            #ifdef UNITY_COLORSPACE_GAMMA
                return float4(col * a, a);
            #else
                float3 b = _PaperBehind.rgb;
                float3 m = GammaToLinearSpace(lerp(LinearToGammaSpace(b), LinearToGammaSpace(col), a));
                return float4(max(m - (1 - a) * b, 0), a);
            #endif
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                bool live = _Print > 0.5 && _Print < 1.5 && _Live > 0.0005;            // k75a3: a print layer in focus moves (at rest: as before)
                float2 uv = live ? LiveUV(i.uv) : i.uv;
                float4 c = tex2D(_MainTex, uv);                                         // premultiplied by its coverage
                if (_Print > 2.5) return HaloMix(c, i.color);                           // k83: the VS sign's halo, mixed as drawn
                if (_Print > 1.5)                                                       // k75a3: the VS sign (premultiplied; additive light where alpha is 0)
                {
                    float3 vrgb = c.rgb + _SweepTone.rgb * SweepBand(i.uv) * _Sweep.z * c.a;
                    return fixed4(vrgb * i.color.rgb * i.color.a, c.a * i.color.a);
                }
                if (_Print > 0.5)                                                       // k75a2: a print layer (straight alpha): its paper aged
                {
                    float paper = PaperOf(c.rgb);
                    float3 prgb = lerp(c.rgb, Aged(c.rgb, SeeOf(uv, c)), paper) * i.color.rgb;   // k105: the mid layer's white print keeps its share
                    if (live) prgb += LiveLight(uv, i.uv, paper) * i.color.rgb;         // k75a3: the eyes' glow, the light over the paper
                    float pa = c.a * i.color.a;
                    return fixed4(prgb * pa, pa);
                }
                if (_Fill.w > 0) c = Filled(i.uv, c);                                   // k75a4: EvilZod's cape down to the band
                float rim = 0;
                if (_RimPx > 0.01)
                {
                    float2 t = _MainTex_TexelSize.xy * _RimPx;
                    float2 d = t * 0.70710678;
                    rim = max(rim, FigA(i.uv + float2( t.x, 0)));                          // k75a4: the coverage as filled
                    rim = max(rim, FigA(i.uv + float2(-t.x, 0)));
                    rim = max(rim, FigA(i.uv + float2(0,  t.y)));
                    rim = max(rim, FigA(i.uv + float2(0, -t.y)));
                    rim = max(rim, FigA(i.uv + d));
                    rim = max(rim, FigA(i.uv - d));
                    rim = max(rim, FigA(i.uv + float2(d.x, -d.y)));
                    rim = max(rim, FigA(i.uv + float2(-d.x, d.y)));
                    rim = saturate(rim * 1.25) * _RimAlpha;
                }
                float under = rim * (1 - c.a);                                          // the rim only where the figure does not cover
                float a = c.a + under;
                float3 rgb = c.rgb + Aged(_RimColor.rgb, _PaperSee) * under;             // k75a2: the rim aged as the paper (k105: its share)
                rgb += FigureFx(i.uv, c);                                               // k75a4: the glasses' glint / the eyes' glow
                rgb *= i.color.rgb * i.color.a;                                         // tint + fade, premultiplied
                a *= i.color.a;
                return fixed4(rgb, a);
            }
            ENDCG
        }
    }
}
