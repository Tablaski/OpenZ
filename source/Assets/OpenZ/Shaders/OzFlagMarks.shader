//   - the enemy flag's day ring = the capture beacon's look (View/FlagBeacons.cs) in the owner's ground-disc colour, subtle: a ring that
//     breathes (0.75 + 0.25 sin) and a wave that spreads from it to 1.8 x its radius every period, fading (1 - w)^2 x 0.7, both with
//     the beacon's glow-ring profile (0 at 0.84 R, 0.55 at 0.95 R, 1 at 0.985 R, 0 at 1.05 R); x (1 - the flags' night). Drawn per
//     pixel from the distance to the pole on a polar grid the view drapes over the rendered ground (the neutral waves' grid,
//     OzNeutralFlagWaves: the grid only carries the ground's shape; uv1.z = its mask, 0 over water / down a step)
//   - the lamp's halo: a soft round glow on a camera-facing quad at the ball, blinking with the ball (OzFlagLamp.hlsl); x the night
// Per vertex: COLOR = colour (linear: ring = team disc colour, halo = team light colour) + a = the element's amount (0..1, faded by the view on captures; 0 = its vertices
// collapse, no pixel runs); uv0 = ring (pole x, pole z, ring radius, 0) / halo (corner x, corner y, half size m, 0);
// uv1 = (0 ring / 1 halo, phase seed in radians, ground mask, 0). Premultiplied: a ring is alpha-blended light, a halo added light.
Shader "OpenZ/FlagMarks"
{
    Properties
    {
        _RingAlpha ("Enemy ring opacity (the capture beacon 0.4)", Range(0, 1)) = 0.35
        _RingGlow ("Enemy ring light gain (HDR, the capture beacon 1.2)", Range(0, 3)) = 1.2
        _RingPeriod ("Enemy ring breath and wave period in s", Float) = 3.2
        _HaloGain ("Lamp halo gain", Range(0, 8)) = 2
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+50" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "FlagMarks"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Offset -2, -4
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "OzFlagLamp.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _RingAlpha;
                float _RingGlow;
                float _RingPeriod;
                float _HaloGain;
                float _ZTest;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float4 color : COLOR; float4 uv0 : TEXCOORD0; float4 uv1 : TEXCOORD1; };
            struct V
            {
                float4 positionCS : SV_POSITION; float4 color : COLOR; float4 shape : TEXCOORD0; float4 kind : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            // the capture beacon's glow ring across its width (FlagBeacons.GlowRing), x = distance / radius
            float RingProfile(float x)
            {
                float rise = x < 0.95 ? 0.55 * saturate((x - 0.84) / 0.11) : 0.55 + 0.45 * saturate((x - 0.95) / 0.035);
                return x < 0.985 ? rise : saturate((1.05 - x) / 0.065);
            }

            V vert(A i)
            {
                V o = (V)0;
                float night = saturate(_OZFlagLook.x);
                bool halo = i.uv1.x > 0.5;
                float amount = i.color.a * (halo ? night : 1.0 - night);
                float3 p = TransformObjectToWorld(i.positionOS.xyz);
                if (halo)
                {   // a camera-facing quad round the ball, pulled 0.4 m toward the camera (the cloth and the pole never cut it); zoomed out
                    // it keeps about its screen size, as a real lamp's bloom does (full size at the normal 70 m camera, 85 m away, up to x2.4)
                    float3 toCam = _WorldSpaceCameraPos - p;
                    float dist = length(toCam); toCam /= max(dist, 1e-3);
                    float grow = clamp(dist / 85.0, 1.0, 2.4);
                    p += (UNITY_MATRIX_V[0].xyz * i.uv0.x + UNITY_MATRIX_V[1].xyz * i.uv0.y) * (i.uv0.z * grow) + toCam * 0.4;
                }
                o.positionWS = p;
                // nothing to show (out, or the wrong time of day): every vertex of the element on one point outside the view
                o.positionCS = amount > 0.002 ? TransformWorldToHClip(p) : float4(-2.0, -2.0, -2.0, 1.0);
                o.color = float4(i.color.rgb, amount);
                o.shape = i.uv0; o.kind = i.uv1;
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                if (i.kind.x > 0.5)
                {   // the lamp's halo
                    float r2 = dot(i.shape.xy, i.shape.xy);
                    float g = exp(-r2 * 5.0) * saturate(1.0 - r2);
                    float k = g * i.color.a * _HaloGain * OZFlagBlink(_Time.y, i.kind.y);
                    return half4(i.color.rgb * k, 0.0);
                }
                // the enemy flag's day ring: the beacon's breathing ring + its wave
                float d = length(i.positionWS.xz - i.shape.xy), R = max(i.shape.z, 0.1);
                float ph = _Time.y / max(_RingPeriod, 0.1) + i.kind.y * 0.1591549;
                float breathe = 0.75 + 0.25 * sin(ph * 6.2831853);
                float w = frac(ph);
                float ring = RingProfile(d / R) * breathe + RingProfile(d / (R * (1.0 + 0.8 * w))) * (1.0 - w) * (1.0 - w) * 0.7;
                float a = saturate(ring * _RingAlpha * i.color.a * saturate(i.kind.z));
                return half4(i.color.rgb * (_RingGlow * a), a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
