// stack of round puffs from the RTS camera). The L02 dust devil's swirling column (View/OzDustDevils.cs): nested open funnel meshes and a
// flat ground swirl ring drawn with a scrolling, twisting streak noise (helical streaks that spin and rise), soft alpha (view-angle edge
// fade, height fade, soft against the ground), lit by the sun + sky (wrap, a soft self shadow on the far side), the ground's dust colour.
// No sprites: nothing round to read at any distance. The funnel leans with the wind / against its travel and sways (vertex shear).
// Premultiplied alpha like OpenZ/FxAdditive; one pass, both faces (the far inner wall shows through the near one).
Shader "OpenZ/DustDevil"
{
    Properties
    {
        _NoiseTex ("Streak noise (R: streaks long in x, G: soft blobs; tileable)", 2D) = "grey" {}
        _Color ("Dust colour (the ground's)", Color) = (0.86, 0.74, 0.56, 1)
        _Alpha ("Opacity", Range(0, 2)) = 0.5
        _Height ("Height (m) of the mesh's v = 1", Float) = 10
        _Twist ("Streak turns along the height (whole turns, integer)", Float) = 2
        _StreakRep ("Streak bands along the height", Float) = 5
        _Spin ("Spin (turns a second) at the foot / top: x, y", Vector) = (0.55, 0.22, 0, 0)
        _Rise ("Streak rise (bands a second)", Float) = 0.6
        _Detail ("Second streak layer: amount", Range(0, 1)) = 0.5
        _Cut ("Streak cut (higher = thinner wisps)", Range(0, 1)) = 0.32
        _Body ("Soft body under the streaks (0 = streaks only)", Range(0, 1)) = 0.35
        _Edge ("View-angle edge softness (silhouettes fade)", Range(0.01, 1)) = 0.55
        _Foot ("Fade-in at the foot (share of the height)", Range(0.001, 0.5)) = 0.04
        _Top ("Fade-out towards the top (share of the height)", Range(0.01, 1)) = 0.6
        _Wrap ("Light wrap", Range(0, 1)) = 0.6
        _Shade ("Self shadow on the far side", Range(0, 1)) = 0.45
        _SoftFade ("Soft against the ground / units (m of eye depth)", Float) = 1.2
        _Lean ("Lean at the top (m, world xz)", Vector) = (0, 0, 0, 0)
        _Sway ("Sway (m at the top) and its speed", Vector) = (0.6, 0.7, 0, 0)
        _Wobble ("Lumpy, twisting outline: radial bulge (share of the radius) and its speed", Vector) = (0.18, 1.6, 0, 0)
        _Ring ("Ground swirl ring mode (uv.y = radius share)", Float) = 0
        _Strength ("Day / night amount", Range(0, 1)) = 1
        _Seed ("Per devil seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        HLSLINCLUDE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseTex_ST;
                half4 _Color;
                half _Alpha, _Detail, _Cut, _Body, _Edge, _Foot, _Top, _Wrap, _Shade, _Strength;
                float _Height, _Twist, _StreakRep, _Rise, _SoftFade, _Ring, _Seed;
                float4 _Spin, _Lean, _Sway, _Wobble;
            CBUFFER_END
            float _OZNight;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; };

            V Vert(A v)
            {
                V o;
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float3 root = TransformObjectToWorld(float3(0, 0, 0));
                float h = saturate((p.y - root.y) / max(_Height, 0.1));
                if (_Ring < 0.5)
                {   // lean (eased in with the height: the foot stays put) and a slow serpentine sway that grows with the height
                    float t = _Time.y * _Sway.y + _Seed;
                    float hh = h * h;
                    p.xz += _Lean.xz * hh;
                    p.x += sin(t * 1.3 + h * 3.1) * _Sway.x * h;
                    p.z += cos(t * 1.07 + h * 2.6 + 1.7) * _Sway.x * h;
                    // a lumpy outline that twists round with the spin: bulges along the radius (no clean cone)
                    float2 rad = v.positionOS.xz;                                   // the devil's root is never turned or scaled
                    float tw = _Time.y * _Wobble.y;
                    float bulge = sin(v.uv.x * 18.85 + h * 5.0 - tw * 2.0 + _Seed) * 0.55 + sin(v.uv.x * 12.57 - h * 7.0 + tw * 1.3 + _Seed * 2.3) * 0.45;
                    p.xz += rad * (_Wobble.x * bulge * (0.35 + 0.65 * h));
                }
                o.positionWS = p;
                o.positionCS = TransformWorldToHClip(p);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }

            half4 Frag(V i, bool front : SV_IsFrontFace) : SV_Target
            {
                float a = i.uv.x;                  // around (0..1, whole turns)
                float h = i.uv.y;                  // up the funnel (0 foot .. 1 top); ring: 0 inner .. 1 outer
                float t = _Time.y;
                float spin = lerp(_Spin.x, _Spin.y, h);
                // helical streaks: the noise's long axis runs round the column; its across-coordinate climbs with the angle (_Twist whole
                // turns keep the seam closed), so lines of equal noise are spirals; they spin round and rise
                float2 uv1 = float2(a + t * spin + _Seed * 0.37, h * _StreakRep + a * _Twist - t * _Rise + _Seed * 0.11);
                float2 uv2 = float2(a * 2.0 - t * spin * 1.6 + _Seed * 0.71, h * _StreakRep * 2.3 + a * (_Twist + 1.0) * 2.0 - t * _Rise * 1.7);
                half n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv1).r;
                half n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv2).r;
                half blob = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, float2(a * 2.0 + t * spin * 0.5, h * 1.3 - t * _Rise * 0.25 + _Seed)).g;
                half dens = saturate((n1 * (1.0 - _Detail * 0.5) + n2 * _Detail - _Cut) / max(1e-3, 1.0 - _Cut));
                dens = (_Body + (1.0 - _Body) * dens) * lerp((half)0.5, (half)1.0, blob);   // a soft body with the streaks over it, big slow clumps
                half cover;
                float3 n = normalize(i.normalWS);
                float3 vdir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                if (_Ring < 0.5)
                {
                    half foot = (half)smoothstep(0.0, _Foot, h);
                    half top = (half)(1.0 - smoothstep(1.0 - _Top, 1.0, h));
                    // the silhouette (the wall seen edge-on) fades: no hard outline. Measured on the wall's horizontal normal against the view,
                    // as a share of the most the camera's pitch allows (the RTS camera looks 55 deg down: front and back walls stay whole)
                    float3 nh = normalize(float3(n.x, 0.0, n.z) + 1e-5);
                    half side = (half)(abs(dot(nh, vdir)) / max(0.2, sqrt(saturate(1.0 - vdir.y * vdir.y))));
                    half edge = (half)smoothstep(0.0, _Edge, side * (0.55 + 0.9 * n2));   // broken up by the streaks: no clean cone line
                    cover = dens * foot * top * edge;
                }
                else
                {   // ground swirl: soft annulus, denser near the foot
                    half inner = (half)smoothstep(0.0, 0.25, h);
                    half outer = (half)(1.0 - smoothstep(0.35, 1.0, h));
                    cover = dens * inner * outer;
                }
                // soft against the ground and anything standing in it
                float2 suv = i.positionCS.xy / _ScaledScreenParams.xy;
                if (_SoftFade > 0.0)
                {
                    float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                    float fragEye = -TransformWorldToView(i.positionWS).z;
                    cover *= (half)saturate((sceneEye - fragEye) / _SoftFade);
                }
                cover = saturate(cover * _Alpha * _Strength);
                // light: sun with wrap + a soft shadow on the side away from it, sky ambient; never glows at night
                Light L = GetMainLight();
                float3 ln = n; ln.y = _Ring > 0.5 ? 1.0 : ln.y * 0.5 + 0.25;
                ln = normalize(ln);
                half ndlRaw = dot(ln, L.direction);
                half ndl = saturate((ndlRaw + _Wrap) / (1.0 + _Wrap));
                ndl *= lerp((half)1.0, (half)smoothstep(-0.6, 0.6, ndlRaw), _Shade);
                half3 light = L.color * ndl + SampleSH(half3(0, 1, 0)) * 1.1;
                half3 albedo = _Color.rgb * (0.9 + 0.3 * n1);
                half3 rgb = albedo * light;
                half fogFactor = InitializeInputDataFog(float4(i.positionWS, 1.0), 0.0);
                half fogVis = MixFog(half3(1.0, 1.0, 1.0), fogFactor).r - MixFog(half3(0.0, 0.0, 0.0), fogFactor).r;
                return half4(rgb * cover * fogVis + unity_FogColor.rgb * ((1.0 - fogVis) * cover), cover);
            }
        ENDHLSL
        Pass
        {
            Name "DustDevilBack"
            Tags { "LightMode"="SRPDefaultUnlit" }   // the far wall first (URP draws this pass before UniversalForward)
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Front

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
        Pass
        {
            Name "DustDevilFront"
            Tags { "LightMode"="UniversalForward" }   // then the near wall over it: no order-dependent bands
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
