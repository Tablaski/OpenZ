// OpenZ/LavaBomb (version 1.3, level 7, LAVA_EFFECTS_PLAN.md section 5 stage 4): the rocks an eruption throws, drawn by OzVolcano with DrawMeshInstanced, one temperature per rock
// (_BombT). A dark lump lit by the sun, cracked open along noise lines; the cracks and then the whole lump glow by temperature on the one ramp, so a fresh rock is molten and a landed
// chunk cools to plain rock. No texture of its own but the noise. In the transparent queue with ZWrite on, so it has no need of a depth pass.
// Row 52 (2 Nov): the light of everything hotter than a dull red is lifted by _HotBloom (x day gain, y night gain, from T z, full at T w), so the hot cracks and a fresh
// surface just out of the crater pass the bloom threshold (by day linear 1.63, OpenZBloomDirector) and the halo fades with the rock's own temperature. The colour of each
// pixel keeps its hue (one gain on the ramp's colour); the crust under T z is not touched, so the cracks stay readable. x = y = 0 gives the look before (the lift is then exactly 1).
Shader "OpenZ/LavaBomb"
{
    Properties
    {
        _NoiseTex ("Noise", 2D) = "gray" {}
        _BaseColor ("Rock", Color) = (0.085, 0.066, 0.06, 1)
        [PerRendererData] _BombT ("Temperature", Float) = 1
        _HotBloom ("Hot bloom: day gain, night gain, from T, full at T", Vector) = (1.2, 0.48, 0.45, 0.8)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-15" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "LavaBomb"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "OzLavaRamp.hlsl"

            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            half _OZNight;
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _HotBloom;
            CBUFFER_END
            UNITY_INSTANCING_BUFFER_START(BombProps)
                UNITY_DEFINE_INSTANCED_PROP(float, _BombT)
            UNITY_INSTANCING_BUFFER_END(BombProps)

            struct Attributes { float3 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 obj : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                o.positionCS = TransformObjectToHClip(i.positionOS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.obj = i.positionOS;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float T0 = UNITY_ACCESS_INSTANCED_PROP(BombProps, _BombT);
                float n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.obj.xz * 0.42 + i.obj.y * 0.31).r;
                float n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.obj.zy * 0.9 + 0.37).r;
                float crack = 1.0 - saturate(abs(n - 0.5) * 7.0);                     // thin lines where the noise crosses its middle
                crack = max(crack, 1.0 - saturate(abs(n2 - 0.5) * 9.0));
                half T = (half)(T0 * lerp(0.42, 1.22, crack));
                half3 N = normalize((half3)i.normalWS);
                Light sun = GetMainLight();
                half lit = saturate(dot(N, (half3)sun.direction)) * 0.9h + 0.28h;
                half glow = OzLavaGlow(T);
                half3 rock = _BaseColor.rgb * (0.7h + 0.6h * (half)n) * sun.color * lit;
                half lift = 1.0h + lerp(_HotBloom.x, _HotBloom.y, saturate(_OZNight)) * smoothstep(_HotBloom.z, _HotBloom.w, T);   // row 52: hot enough to bloom, less as it cools
                return half4(rock * (1.0h - glow) + OzLavaEmit(T, _OZNight) * lift, 1.0h);
            }
            ENDHLSL
        }
    }
}
