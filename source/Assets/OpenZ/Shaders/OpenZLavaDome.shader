// Level 7, kit LV2: a lava bubble rising (View/OzLavaDomes.cs draws it, instanced, 32 at most, about 60 triangles each). Per instance _OZDome:
// x = swell 0..1, y = own random, z = the lava's heat now. The dome is lit crust at its foot; as it swells the skin stretches, cracks open
// bright from the top down, and the last moment before the burst the top goes to core. Every hot colour comes from the one ramp
// (OzLavaRamp.hlsl). No texture. Holds still in a pause: u comes from the script.
Shader "OpenZ/LavaDome"
{
    Properties
    {
        _CrackScale ("Cracks across the dome", Float) = 5.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+11" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float _CrackScale;
        CBUFFER_END
        UNITY_INSTANCING_BUFFER_START(OzLavaDomeProps)
            UNITY_DEFINE_INSTANCED_PROP(float4, _OZDome)
        UNITY_INSTANCING_BUFFER_END(OzLavaDomeProps)
        half _OZNight;
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On ZTest LEqual Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "OzLavaRamp.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 objectPos : TEXCOORD0; float3 normalWS : TEXCOORD1; half fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.objectPos = v.positionOS.xyz;
                float3 w = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(w);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float Hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 d = UNITY_ACCESS_INSTANCED_PROP(OzLavaDomeProps, _OZDome);
                float u = d.x, top = saturate(i.objectPos.y);
                float2 q = i.objectPos.xz * _CrackScale + d.y * 37.0;
                float n = (Noise(q) + 0.5 * Noise(q * 2.3 + 5.1)) / 1.5;
                float crack = 1.0 - smoothstep(0.0, 0.05 + 0.12 * u, abs(n - 0.5));                  // thin at first, wide as the skin stretches
                float open = smoothstep(0.12, 0.85, top * (0.35 + 0.95 * u));
                half skinT = (half)((0.30 + 0.42 * u) * lerp(0.55, 1.3, saturate((Noise(q * 1.7 + 2.0) - 0.3) / 0.4)));   // molten skin with dark flecks of crust riding on it, hotter as it stretches
                half T = (half)(skinT + crack * open * (0.78 + 0.3 * u));
                T = T * (half)d.z + (half)(smoothstep(0.78, 1.0, u) * top * 0.95);
                T = max(T, (half)((1.0 - smoothstep(0.0, 0.26, top)) * (0.82 + 0.40 * u)));          // the bright rim where the crust tears round the foot: this is what reads from the game camera
                Light L = GetMainLight();
                half3 N = normalize(i.normalWS);
                half lit = (half)saturate(dot(N, L.direction));
                half3 crust = lerp((half3)_OZLavaRamp[0].rgb, (half3)_OZLavaRamp[1].rgb, (half)(n * n)) * 0.7;
                half3 c = crust * (0.35 + L.color * lit) * (1.0 - OzLavaGlow(T)) + OzLavaEmit(T, _OZNight);
                c = MixFog(c, i.fogFactor);
                return half4(c, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes v) { Varyings o; UNITY_SETUP_INSTANCE_ID(v); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
