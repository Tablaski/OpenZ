// OpenZ/BridgeRestore: the restore of a destroyed bridge (View/OzBridgeRestore.cs, 1.5 kit bridge_restore_r3). One shader, two looks:
//   _Mode 0 HOLO   the Unit editor's hologram look (Shaders/OzUeHolo.shader, its "seen" faces: a faint fill, a 1 m grid, neon edges with one
//                  soft added glow, a slow scan line) on any mesh, tinted construction orange mixed toward a warm pale: the pieces flying in,
//                  the faint outline of the whole bridge they fly into, the wreck turning into light. The neon edges are the piece's own cut
//                  planes (_Cut: along min / max, across min / max in the piece's own coordinates, metres; far away = no edge), plus a
//                  view rim so the silhouette reads like the hologram box's edges.
//   _Mode 1 ENERGY the construction-orange energy fill over the solid deck (the flag capture's zone wash, Shaders/OpenZFlagCaptureFx.shader
//                  mode 1, laid along the bridge): from BOTH ends toward the middle a front (_Front.x, 0 = the ends, 1 = the middle) with a
//                  bright band, a glowing trail behind it and a light orange tint, energy streaks running along the deck, a white-hot
//                  flash per piece as it locks (_Hit); _Gain fades it all as the bridge becomes normal.
// Never a team colour. Premultiplied alpha (Blend One OneMinusSrcAlpha), no depth write, both sides.
// Per renderer (MaterialPropertyBlock): _Alpha, _Hit, _Cut, _Axis (xyz the deck's long axis in world space, w half its length m), _AxisB
// (xyz the across axis), _Centre (xyz the deck's middle), _Front (x the front 0..1, y the band width, z the tint, w the band strength), _Gain,
// _Clock (s).
Shader "OpenZ/BridgeRestore"
{
    Properties
    {
        _Mode ("Look: 0 hologram, 1 energy fill", Float) = 0
        _Color ("Construction orange (linear)", Color) = (1.0, 0.36, 0.06, 1)
        _Pale ("Hologram pale (linear)", Color) = (1.0, 0.88, 0.72, 1)
        _PaleMix ("Hologram: share toward the pale", Range(0, 1)) = 0.35
        _Alpha ("Opacity 0..1 - per renderer", Range(0, 1)) = 1
        _Hit ("Lock flash 0..1 - per renderer", Range(0, 1)) = 0
        _Cut ("Cut planes (along min, max, across min, max) in the piece's metres - per renderer", Vector) = (-10000, 10000, -10000, 10000)
        _Axis ("Deck long axis (xyz world), half length (w)", Vector) = (1, 0, 0, 10)
        _AxisB ("Deck across axis (xyz world)", Vector) = (0, 0, 1, 0)
        _Centre ("Deck middle (xyz world)", Vector) = (0, 0, 0, 0)
        _Front ("Fill: front 0..1, band width, tint, band strength", Vector) = (0, 0.12, 0.2, 1)
        _Gain ("Overall strength", Float) = 1
        _Clock ("Clock (s)", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+41" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "BridgeRestore"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off
            Offset -1, -2
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Mode;
                float4 _Color;
                float4 _Pale;
                float _PaleMix;
                float _Alpha;
                float _Hit;
                float4 _Cut;
                float4 _Axis;
                float4 _AxisB;
                float4 _Centre;
                float4 _Front;
                float _Gain;
                float _Clock;
                float _ZTest;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 cutAB : TEXCOORD2;          // the piece's own along / across coordinate (m): the object space is the world's at rest
            };

            V vert(A i)
            {
                V o = (V)0;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.cutAB = float2(dot(i.positionOS.xyz, _Axis.xyz), dot(i.positionOS.xyz, _AxisB.xyz));
                return o;
            }

            float Grid(float2 c, float2 fw)
            {
                float2 g = abs(frac(c - 0.5) - 0.5) / fw;
                return 1.0 - saturate(min(g.x, g.y));
            }
            float4 Over(float4 below, float3 rgb, float a) { return float4(rgb * a + below.rgb * (1.0 - a), a + below.a * (1.0 - a)); }
            float Hash(float x) { return frac(sin(x * 127.1 + 31.7) * 43758.5453); }

            half4 frag(V i, bool ff : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(i.normalWS) * (ff ? 1.0 : -1.0);
                float3 v = normalize(_WorldSpaceCameraPos - i.positionWS);
                float fres = pow(1.0 - saturate(abs(dot(n, v))), 2.0);
                float hit = saturate(_Hit);
                float3 orange = _Color.rgb;
                // 0 at either end of the deck, 1 in its middle
                float s = dot(i.positionWS - _Centre.xyz, _Axis.xyz) / max(_Axis.w, 0.1);
                float u = 1.0 - saturate(abs(s));
                if (_Mode < 0.5)
                {   // ---------------------------------------------------------------- HOLO
                    float3 holo = lerp(orange, _Pale.rgb, _PaleMix);
                    float4 col = 0;
                    col = Over(col, holo, 0.10 + 0.30 * fres + 0.12 * hit);
                    // the 1 m grid on the plane the face lies in (world metres)
                    float3 an = abs(n);
                    float2 gc = an.y > 0.6 ? i.positionWS.xz : (an.x > an.z ? i.positionWS.zy : i.positionWS.xy);
                    col = Over(col, holo, (0.20 + 0.25 * hit) * Grid(gc, max(fwidth(gc), 1e-4)));
                    // the scan: a line running from the ends toward the middle
                    float fu = max(fwidth(u), 1e-4);
                    float scan = frac(_Clock * 0.9);
                    col = Over(col, holo, 0.45 * saturate(1.2 * 0.5 + 0.5 - abs(u - scan) / fu));
                    // neon edges on the piece's cut planes (the seams of the puzzle) + one soft glow; the view rim for the silhouette
                    float2 c = i.cutAB, fw = max(fwidth(c), 1e-4);
                    float eA = min(abs(c.x - _Cut.x), abs(c.x - _Cut.y)) / fw.x;
                    float eB = min(abs(c.y - _Cut.z), abs(c.y - _Cut.w)) / fw.y;
                    float e = min(eA, eB);
                    col.rgb += holo * (0.16 + 0.2 * hit) * saturate(1.0 - e / 2.5);
                    col = Over(col, holo, 0.95 * saturate(1.4 * 0.5 + 0.5 - e));
                    col = Over(col, holo, 0.55 * smoothstep(0.55, 0.95, fres));
                    col *= saturate(_Alpha);
                    return half4(col.rgb, col.a);
                }
                // -------------------------------------------------------------------- ENERGY
                float F = _Front.x, bw = max(_Front.y, 0.02);
                float inside = 1.0 - smoothstep(F - 0.04, F + 0.02, u);                 // behind the front (nearer the end it came from)
                float band = exp(-((u - F) * (u - F)) / (bw * bw)) * _Front.w;
                float trail = exp(-max(F - u, 0.0) / 0.3) * inside;
                float tint = inside * _Front.z;
                // energy streaks running along the deck toward the middle (lanes across the deck, each its own phase)
                float along = dot(i.positionWS - _Centre.xyz, _Axis.xyz), across = dot(i.positionWS - _Centre.xyz, _AxisB.xyz);
                float lane = floor(across * 2.2);
                float flow = (along < 0.0 ? along : -along) * 1.6 + _Clock * 9.0 + Hash(lane) * 6.283;
                float streak = pow(0.5 + 0.5 * sin(flow), 10.0) * inside;
                float3 rgb = orange * (tint * 0.5 + trail * 0.75 + band * 2.4 + streak * 0.55 + fres * inside * 0.35)
                           + orange * hit * 1.6 + float3(1.0, 0.85, 0.6) * hit * hit * 0.5;
                float a = saturate(tint * 0.42 + hit * 0.25);
                float g = saturate(_Gain);
                return half4(rgb * g, a * g);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
