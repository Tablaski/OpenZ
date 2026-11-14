// OpenZ (GPL-3.0) - version 1.3, level 7 (kit LV3): the hot footprints on the bank (View/OzLavaPrints.cs). One joined mesh; each quad carries its
// birth on the lava clock, so the cooling costs no script. uv -1..1 over the print; uv1 x birth, y a random number. Colour from the one ramp.
Shader "OpenZ/LavaPrint"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "OzLavaRamp.hlsl"
            float _OZLavaTime;
            half _OZNight;
            #define OZ_PRINT_LIFE 5.0
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 birth : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 birth : TEXCOORD1; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv; o.birth = v.birth;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float age = (_OZLavaTime - i.birth.x) / OZ_PRINT_LIFE;
                if (age < 0.0 || age >= 1.0) discard;
                // the foot: a broad toe and a smaller heel, rough at the rim by two sine bands
                float2 q = i.uv;
                float toe = length((q - float2(0.0, 0.32)) * float2(1.05, 1.5));
                float heel = length((q - float2(0.0, -0.48)) * float2(1.35, 2.0));
                float rough = 0.10 * sin(q.x * 13.0 + i.birth.y * 40.0) * sin(q.y * 17.0 - i.birth.y * 23.0);
                half shape = 1.0 - smoothstep(0.62, 0.92, min(toe, heel) + rough);
                half core = 1.0 - smoothstep(0.15, 0.85, min(toe, heel) + rough);
                half cool = (half)(age * age * (3.0 - 2.0 * age));
                half T = (0.62 + 0.36 * core) * (1.0 - cool);
                half3 glow = OzLavaEmit(T, _OZNight);
                return half4(glow * shape, shape * 0.78 * (1.0 - cool));
            }
            ENDHLSL
        }
    }
}
