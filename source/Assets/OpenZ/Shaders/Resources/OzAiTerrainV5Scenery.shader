// OpenZ-VK (GPL-3.0).
Shader "OpenZ/AI Map V5 Scenery"
{
    Properties
    {
        _Biome ("Biome", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "../OzAiTerrainV5.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Biome;
            CBUFFER_END
            struct Input { float4 vertex : POSITION; float3 normal : NORMAL; half4 color : COLOR; };
            struct Output { float4 clip : SV_POSITION; float3 world : TEXCOORD0; half3 normal : TEXCOORD1; half4 color : COLOR; };
            Output Vert(Input i)
            {
                Output o;
                o.world = TransformObjectToWorld(i.vertex.xyz);
                o.clip = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(i.normal);
                o.color = i.color;
                return o;
            }

            half4 Frag(Output i) : SV_Target
            {
                const float3 p = i.world;
                half3 n = normalize(i.normal);
                float weather = V5Noise(p.xz * .12);
                float grains = V5Noise((p.xz + p.y * .37) * 3.4);
                float beds = V5Noise(float2(p.x + p.z + weather * 4, p.y * 3.6) * .53);
                half3 rock = i.color.rgb;
                if (_Biome > .5)
                {
                    if (_Biome < 1.5)
                        rock = lerp(half3(.28,.118,.048),half3(.65,.41,.23),saturate(beds*.82 + weather*.29));
                    else if (_Biome < 2.5)
                    {
                        float pores = smoothstep(.60,.83,V5Noise(float2(p.x-p.z,p.y)*6.9));
                        rock = lerp(half3(.069,.061,.072),half3(.22,.15,.14),weather) * (1 - pores*.54);
                    }
                    else if (_Biome < 3.5)
                        rock = lerp(half3(.17,.18,.17),half3(.39,.34,.28),beds);
                    else if (_Biome < 4.5)
                        rock = lerp(half3(.12,.072,.04),half3(.13,.21,.075),smoothstep(.35,.84,n.y + weather*.24));
                    else
                        rock = lerp(lerp(half3(.13,.22,.32),half3(.31,.40,.47),beds),half3(.72,.81,.90),
                            smoothstep(.49,.86,n.y + weather*.22));
                }
                float3 lighting = max(SampleSH(n), .19);
                Light sun = GetMainLight(TransformWorldToShadowCoord(p));
                lighting += sun.color * (saturate(dot(n,sun.direction)) * .55 + .14) * lerp(.58,1,sun.shadowAttenuation);
                return half4(rock * (.86 + grains*.24) * lighting, 1);
            }
            ENDHLSL
        }
    }
}
