// OpenZ/UeHolo: the Unit editor's holographic obstacle (1.2 item 6, owner 6j; View/OzUeObstacleView.cs; design spec E211), a box drawn
// as a clean hologram in the floor style's light colour (_HoloColor = its hi colour mixed 42 % toward #E6EFE9, set by the view):
//   part 0 POOL   a faint light pool on the floor round it (radius 0.9 x the larger side, 10 % from 0.35 R to 0 at R);
//   part 1 BODY   a dark body #050809 at 42 % over the box silhouette (front faces only: a convex box covers each pixel once);
//   part 2 HIDDEN the faces turned away from the camera, faint (back 3.5 %, sides 5 %; the sides' 1 m grid 10 %), their edges faint 30 %;
//   part 3 SEEN   the faces turned to the camera: the top lit 13 %, the front a vertical gradient 14 % (bottom) -> 4 % (top), a side 5 %;
//                 a 1 m grid (front / top 22 %, sides 10 %); neon edges 1.4 px at 95 % with one 5 px soft added glow (16 %), the back
//                 vertical edges of the sides faint; 3 px corner dots; a slow scan line (0.3 cycles / s) rising up the front face and
//                 running over the top (45 %, 1.2 px).
// "Front" = the side face that looks most at the camera (the nearest quarter turn), so a turned camera keeps the look.
// Per obstacle (MaterialPropertyBlock): _BoxSize (w, h, d m), _BoxCentre (world, floor), _Hit (a hit flash, +12 % on the faces for
// 0.18 s), _Alpha (the low-health flicker; the break-up fade), _Rise (the break-up: only the top outline, rising 2.5 m and growing 25 %),
// _Seed (scan phase). Per material: _HoloColor, _HoloTime (the scan clock: it stops while the game is paused).
// Mesh (the view builds it): uv0 = the face's own coordinates in metres (u along the face, v = the height on side faces); uv1 = (face
// width m, face height m, face id: 0..3 the side faces +x -x +z -z, 4 the top, 5 the pool, 0). Premultiplied alpha, no depth write.
Shader "OpenZ/UeHolo"
{
    Properties
    {
        _HoloColor ("Hologram colour (the floor style's light, mixed 42 % toward #E6EFE9)", Color) = (0.78, 0.89, 0.94, 1)
        _HoloTime ("Scan clock (s)", Float) = 0
        _Part ("Part: 0 pool, 1 body, 2 hidden faces, 3 seen faces", Float) = 3
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
        _BoxSize ("Box size (w, h, d m) - per obstacle", Vector) = (6, 3, 6, 0)
        _BoxCentre ("Box floor centre (world) - per obstacle", Vector) = (0, 0, 0, 0)
        _Hit ("Hit flash 0..1 - per obstacle", Range(0, 1)) = 0
        _Alpha ("Opacity 0..1 - per obstacle", Range(0, 1)) = 1
        _Rise ("Break-up 0..1 - per obstacle", Range(0, 1)) = 0
        _Seed ("Scan phase seed - per obstacle", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+40" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "UeHolo"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull [_Cull]
            Offset -1, -2
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _HoloColor;
                float _HoloTime;
                float _Part;
                float _Cull;
                float _ZTest;
                float4 _BoxSize;
                float4 _BoxCentre;
                float _Hit;
                float _Alpha;
                float _Rise;
                float _Seed;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 uv0 : TEXCOORD0; float4 uv1 : TEXCOORD1; };
            struct V
            {
                float4 positionCS : SV_POSITION;
                float4 face : TEXCOORD0;          // face coords (u, v) m, face size (w, h) m
                float4 info : TEXCOORD1;          // face id, -, -, -
                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
            };

            V vert(A i)
            {
                V o = (V)0;
                float3 p = i.positionOS.xyz;
                if (_Rise > 0.0)
                {   // the break-up: the outline grows 25 % and rises 2.5 m while it fades
                    p.xz *= 1.0 + 0.25 * _Rise;
                    p.y += 2.5 * _Rise;
                }
                o.positionWS = TransformObjectToWorld(p);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.face = float4(i.uv0.xy, i.uv1.xy);
                o.info = float4(i.uv1.z, 0, 0, 0);
                return o;
            }

            // a 1 px anti-aliased line at every whole metre of the face (its borders left to the edges)
            float Grid(float2 c, float2 fw)
            {
                float2 g = abs(frac(c - 0.5) - 0.5) / fw;
                return 1.0 - saturate(min(g.x, g.y));
            }
            // one layer of the hologram colour over what is below (premultiplied)
            float4 Over(float4 below, float3 rgb, float a) { return float4(rgb * a + below.rgb * (1.0 - a), a + below.a * (1.0 - a)); }

            half4 frag(V i) : SV_Target
            {
                float3 holo = _HoloColor.rgb;
                float fade = saturate(_Alpha);
                int id = (int)round(i.info.x);
                float2 c = i.face.xy, size = i.face.zw;
                if (id == 5)
                {   // part 0: the pool of light on the floor
                    if (_Rise > 0.0) return 0;
                    float R = 0.9 * max(_BoxSize.x, _BoxSize.z);
                    float r = length(i.positionWS.xz - _BoxCentre.xz) / max(R, 0.01);
                    float a = 0.10 * (1.0 - saturate((r - 0.35) / 0.65)) * step(r, 1.0) * fade;
                    return half4(holo * a, a);
                }
                if (_Part < 0.5) return 0;                                       // the pool material draws only the pool
                float2 fw = max(fwidth(c), 1e-4);
                float2 dE = min(c, size - c) / fw;                               // px from the face's borders, per axis
                float edgePx = min(dE.x, dE.y);
                bool top = id == 4;
                // the camera's side of the box: the front = the side face whose normal looks most at the camera (axis aligned)
                float2 toCam = _WorldSpaceCameraPos.xz - _BoxCentre.xz;
                float2 cd = normalize(toCam + float2(1e-4, 0));
                float2 frontN = abs(cd.x) > abs(cd.y) ? float2(sign(cd.x), 0) : float2(0, sign(cd.y));
                float2 n = normalize(i.normalWS.xz + float2(1e-5, 0));
                float facing = top ? 1.0 : dot(n, frontN);
                bool front = !top && facing > 0.5, back = !top && facing < -0.5, side = !top && !front && !back;
                if (_Part < 1.5)
                {   // part 1: the dark body over the silhouette (front faces only, each pixel once)
                    if (_Rise > 0.0) return 0;
                    float a = 0.42 * fade;
                    return half4(float3(0.0015, 0.0024, 0.0027) * a, a);                // #050809 (sRGB) in linear (the project draws in linear colour)
                }
                float hit = saturate(_Hit);
                float4 col = 0;
                if (_Rise > 0.0)
                {   // the break-up: only the top outline
                    if (!top || _Part < 2.5) return 0;
                    float l = saturate(1.5 * 0.5 + 0.5 - edgePx) * 0.9 * fade;
                    return half4(holo * l, l);
                }
                bool hidden = _Part < 2.5;
                // fills
                float fill = top ? 0.13 + 0.12 * hit
                           : front ? lerp(0.14, 0.04, saturate(c.y / max(size.y, 0.01))) + 0.12 * hit
                           : back ? 0.035 + 0.08 * hit : 0.05 + 0.08 * hit;
                col = Over(col, holo, fill);
                // the 1 m grid: front / top clear, sides faint, none on the back
                float ga = (top || front) ? 0.22 + 0.25 * hit : side ? 0.10 : 0.0;
                if (ga > 0.0) col = Over(col, holo, ga * Grid(c, fw));
                // the scan line: up the front wall, then over the top, away from the camera (derivatives taken outside the branches)
                float H = _BoxSize.y, D = abs(frontN.x) > 0.5 ? _BoxSize.x : _BoxSize.z;
                float depth = dot(_BoxCentre.xz - i.positionWS.xz, frontN) + D * 0.5;           // on the top: 0 at the front edge, D at the back
                float fwd = max(fwidth(depth), 1e-4);
                if (!hidden && (front || top))
                {
                    float ph = frac(_HoloTime * 0.3 + _Seed), up = H / max(H + D, 0.01);
                    float sd = 1e6;
                    if (front && ph < up) sd = abs(c.y - ph / up * H) / fw.y;
                    if (top && ph >= up) sd = abs(depth - (ph - up) / max(1.0 - up, 0.01) * D) / fwd;
                    col = Over(col, holo, 0.45 * saturate(1.2 * 0.5 + 0.5 - sd));
                }
                // edges: bright, or faint where they belong to the back (the back face; a side's vertical edge on the far side)
                bool faint = back;
                if (side && dE.x < dE.y) faint = dot(i.positionWS.xz - _BoxCentre.xz, cd) < 0.0;
                if (hidden) faint = true;
                if (faint) col = Over(col, holo, 0.30 * saturate(1.0 * 0.5 + 0.5 - edgePx));
                else
                {
                    float glow = (0.16 + 0.2 * hit) * saturate(1.0 - edgePx / 2.5);             // one 5 px soft glow, added light
                    col.rgb += holo * glow;
                    col = Over(col, holo, 0.95 * saturate(1.4 * 0.5 + 0.5 - edgePx));
                    // 3 px corner dots: the top's four corners and the front's two on the floor
                    bool dot3 = (top && dE.x < 1.5 && dE.y < 1.5) || (front && c.y < size.y * 0.5 && dE.x < 1.5 && dE.y < 1.5);
                    if (dot3) col = Over(col, holo, 0.95);
                }
                col *= fade;
                return half4(col.rgb, col.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
