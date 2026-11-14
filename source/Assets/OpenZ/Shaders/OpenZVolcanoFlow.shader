// OpenZ/VolcanoFlow (version 1.3, level 7, LAVA_EFFECTS_PLAN.md section 5 stage 6, fix 12): the lava running down a cone in an eruption. A second renderer over the same mesh, one
// per LOD, on only during an eruption (OzVolcano). The mesh's own UV reads T_L07_VolFlow / T_L07_DecorFlow: R flow time from the Blender pass (0 rim .. 1 foot), G gully strength (the
// downhill accumulation, so the lava runs in the real gullies and not as a ring), B painted texels. A front runs down, the fresh lava is wide and bright, then a crust closes over it
// from the edges and it cools down the one ramp, ending as a dark skin that thins away. Premultiplied: hot parts add light, the crust covers the rock.
// Per renderer (property block): _FlowMap, _FlowFront s since the burst, _FlowRun s from rim to foot, _FlowReach how far down it gets 0..1, _FlowCool s to crust, _FlowFade, _FlowSeed.
Shader "OpenZ/VolcanoFlow"
{
    Properties
    {
        _FlowMap ("Flow map: R time, G gully, B cover", 2D) = "black" {}
        _NoiseTex ("Noise", 2D) = "gray" {}
        _Inflate ("Lift off the rock (m)", Float) = 0.02
        [HideInInspector] _FlowFront ("Front", Float) = 0
        [HideInInspector] _FlowRun ("Run", Float) = 3
        [HideInInspector] _FlowReach ("Reach", Float) = 1
        [HideInInspector] _FlowCool ("Cool", Float) = 25
        [HideInInspector] _FlowFade ("Fade", Float) = 1
        [HideInInspector] _FlowSeed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-20" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "VolcanoFlow"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "OzLavaRamp.hlsl"

            TEXTURE2D(_FlowMap); SAMPLER(sampler_FlowMap);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            half _OZNight;
            float _OZLavaTime;
            float _OZLavaMood;
            CBUFFER_START(UnityPerMaterial)
                float _Inflate, _FlowFront, _FlowRun, _FlowReach, _FlowCool, _FlowFade, _FlowSeed;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 nuv : TEXCOORD1; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(i.positionOS + i.normalOS * _Inflate);
                o.positionCS = TransformWorldToHClip(ws);
                o.uv = i.uv;
                o.nuv = i.uv * 8.0 + _FlowSeed;              // the atlas cells are the same size on every cone, so the noise is too
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 f = SAMPLE_TEXTURE2D(_FlowMap, sampler_FlowMap, i.uv).rgb;
                if (f.b < 0.5) discard;
                float n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.nuv).r;
                float n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.nuv * 2.7 + float2(0.0, _OZLavaTime * -0.05)).r;
                float age = _FlowFront - (f.r + (n - 0.5) * 0.08) * _FlowRun;         // seconds since the front passed here; the noise tears the front's edge
                if (age <= 0.0 || f.r > _FlowReach + (n - 0.5) * 0.12) discard;
                float fresh = saturate(1.0 - age / 3.0), rimWide = saturate(1.0 - f.r * 4.0);
                float gully = f.g + (n - 0.5) * 0.34;
                float line0 = 0.56 - 0.16 * fresh - 0.26 * rimWide;                    // wide near the crater and while fresh, then it draws back into the gully
                half mask = (half)smoothstep(line0, line0 + 0.12, gully);
                if (mask < 0.01) discard;
                float cool = saturate(age / max(_FlowCool, 1.0));
                float crust = smoothstep(0.56, 0.78, n2 + cool * 0.9);                // the crust closes in patches, cracks stay open longest
                float T = lerp(1.12, 0.62, smoothstep(0.0, 0.3, cool)) * (1.0 - crust) + lerp(0.58, 0.04, smoothstep(0.1, 0.8, cool)) * crust;
                T *= 0.86 + 0.14 * (float)mask;
                T *= 1.0 + 0.3 * (_OZLavaMood - 1.0);                                 // breathes with the lakes
                half glow = OzLavaGlow((half)T);
                half3 skin = OzLavaRamp((half)min(T, 0.25)) * lerp(1.0h, 0.35h, saturate(_OZNight));
                half3 rgb = OzLavaEmit((half)T, _OZNight) + skin * (1.0h - glow);
                half fade = (half)_FlowFade;
                return half4(rgb * mask * fade, mask * fade * (0.55h + 0.4h * (1.0h - glow)));
            }
            ENDHLSL
        }
    }
}
