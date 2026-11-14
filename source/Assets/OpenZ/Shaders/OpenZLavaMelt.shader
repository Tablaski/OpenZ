// OpenZ/LavaMelt (version 1.3, level 7, kit LV5): the glow of a piece melting into the lava, for what does not draw with TeamLit. A second draw of the piece's own mesh on top of its own
// look, so nothing is swapped and nothing pops: at heat 0 it adds nothing. The recipe is TeamLit's for robot legs (OzLavaWade), same numbers, same one ramp: dark hot metal and soot, the
// ramp climbing from the lava line (_OZLavaY) with a broken edge, the red over all of it. OzLavaMelt draws it two ways: instanced for FxManager's rubble chips (per-instance _MeltT, _MeltTop),
// and one piece at a time with a block. Premultiplied, no depth written, so the sheet still covers what has gone under. No keyword, no texture.
Shader "OpenZ/LavaMelt"
{
    Properties
    {
        [PerRendererData] _MeltT ("Heat", Float) = 0
        [PerRendererData] _MeltTop ("Height of the glow over the lava (m)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-20" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "LavaMelt"
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
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "OzLavaRamp.hlsl"

            half _OZNight;
            float _OZLavaY;
            UNITY_INSTANCING_BUFFER_START(MeltProps)
                UNITY_DEFINE_INSTANCED_PROP(float, _MeltT)
                UNITY_DEFINE_INSTANCED_PROP(float, _MeltTop)
            UNITY_INSTANCING_BUFFER_END(MeltProps)

            struct Attributes { float3 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            half MeltCell(float2 c) { return (half)frac(sin(dot(c, float2(12.9898, 78.233))) * 43758.5453); }
            half MeltNoise(float3 p)
            {
                float2 q = float2(p.x + p.z, p.y) * float2(5.0, 9.0), c = floor(q), f = q - c; f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(MeltCell(c), MeltCell(c + float2(1, 0)), (half)f.x), lerp(MeltCell(c + float2(0, 1)), MeltCell(c + float2(1, 1)), (half)f.x), (half)f.y);
            }

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                o.positionWS = TransformObjectToWorld(i.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half lg = (half)saturate(UNITY_ACCESS_INSTANCED_PROP(MeltProps, _MeltT));
                float top = max(UNITY_ACCESS_INSTANCED_PROP(MeltProps, _MeltTop), 0.05);
                if (lg < 0.001) return half4(0, 0, 0, 0);
                half ln = MeltNoise(i.positionWS);
                half lu = (half)((i.positionWS.y - _OZLavaY) / top) + (ln - 0.5) * 0.30;
                half glow = 1.0 - smoothstep(0.70, 1.0, lu), soot = 1.0 - smoothstep(1.0, 1.7, lu);
                half3 c = OzLavaEmit(lerp(1.05, 0.68, saturate(lu / 0.8)) * lg * (0.92 + 0.16 * ln), _OZNight) * glow * 3.2;
                half hh = lg * 0.55, up = saturate((half)(i.positionWS.y - _OZLavaY) / 2.4), reach = saturate((hh * 1.25 - up) * 3.0);   // OzLavaMelt.PeakHeat, TeamLit's red over all of it
                c += half3(1.0, 0.27, 0.05) * (reach * (0.6 + 2.4 * hh * hh)) * lg;
                return half4(c, 0.72 * saturate(lg * 1.6) * max(glow, soot));
            }
            ENDHLSL
        }
    }
}
