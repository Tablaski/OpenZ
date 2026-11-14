// OpenZ (GPL-3.0) - version 1.3, level 7, blue briefing: EvilZod and BS-01 are not TeamLit, so their legs in the lava get this second draw of the same skinned mesh: hot metal, the look of
// TeamLit's _OZLegHeat (lava_kits LV3) for these two. From the lava line up: core at the line, dull red above, a smooth broken edge, soot and darkened steel over it, the plates of the
// body's own picture kept readable. One pass: rgb is added, alpha darkens what is under it (Blend One SrcAlpha), so it can both glow and blacken.
// Per robot: _Line world y of the lava, _Top height of the band in m, _Heat 0..1, _BaseMap the body's picture. The ramp is the one ramp of everything hot, the global _OZLavaRamp
// (OzLavaRampTable): read here, not included, so this file compiles whatever order the kits go in. Nothing but the briefing's two bodies uses it.
Shader "OpenZ/L07LegHot"
{
    Properties
    {
        [HideInInspector] _BaseMap ("Body picture", 2D) = "white" {}
        _Line ("Lava line, world y", Float) = 0
        _Top ("Band, m", Float) = 0.6
        _Heat ("Heat", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend One SrcAlpha
            ZWrite Off ZTest LEqual Cull Back Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float _Line, _Top, _Heat;
            CBUFFER_END
            float4 _OZLavaRamp[5];
            half _OZNight;
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float3 w : TEXCOORD0; float2 uv : TEXCOORD1; };
            V vert(A i) { V o; o.w = TransformObjectToWorld(i.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.w); o.uv = i.uv; return o; }
            half Cell(float2 c) { return (half)frac(sin(dot(c, float2(127.1, 311.7))) * 43758.5453); }
            half Smooth(float3 p)
            {   // TeamLit's OZLegNoise: soft cells, no blocks
                float2 q = float2(p.x + p.z, p.y) * float2(5.0, 9.0), c = floor(q), f = q - c; f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Cell(c), Cell(c + float2(1, 0)), (half)f.x), lerp(Cell(c + float2(0, 1)), Cell(c + float2(1, 1)), (half)f.x), (half)f.y);
            }
            half3 Ramp(half T)
            {   // OzLavaRamp.hlsl
                half s = saturate(T) * 4.0; int k = min((int)s, 3); half f = s - (half)k;
                half3 a = (half3)_OZLavaRamp[k].rgb * (half)_OZLavaRamp[k].a, b = (half3)_OZLavaRamp[k + 1].rgb * (half)_OZLavaRamp[k + 1].a;
                return lerp(a, b, f) * (1.0 + 1.6 * max(T - 1.0, 0.0));
            }
            half4 frag(V i) : SV_Target
            {
                half n = Smooth(i.w), lum = dot(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb, half3(0.2126, 0.7152, 0.0722));
                half u = (half)((i.w.y - _Line) / max(_Top, 0.05)) + (n - 0.5) * 0.30;
                half glow = 1.0 - smoothstep(0.70, 1.0, u), soot = 1.0 - smoothstep(1.0, 1.4, u);                   // TeamLit's glow; the soot stops sooner, so it ends under the fists
                half T = lerp(0.62, 0.42, saturate(u / 0.8)) * (half)_Heat * (0.92 + 0.16 * n);
                half3 c = Ramp(T) * smoothstep(0.25, 0.5, T) * (1.0 + 0.15 * saturate(_OZNight)) * glow * 2.0 * lerp(0.55, 1.2, saturate(lum * 2.2));   // the plates stay readable in the heat
                return half4(c, 1.0 - 0.72 * saturate((half)_Heat * 1.6) * max(glow, soot));
            }
            ENDHLSL
        }
    }
}
