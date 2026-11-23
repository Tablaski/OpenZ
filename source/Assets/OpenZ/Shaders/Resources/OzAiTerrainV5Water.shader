// OpenZ-VK (GPL-3.0).
Shader "OpenZ/AI Map V5 Water Detail"
{
    Properties { _Pouring ("Runoff", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+2" }
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../OzAiTerrainV5.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Pouring;
            CBUFFER_END
            struct Input { float4 position : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Output { float4 clip : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            Output Vert(Input i)
            {
                Output o;
                o.clip = TransformObjectToHClip(i.position.xyz);
                o.color = i.color;
                o.uv = i.uv;
                return o;
            }
            half4 Frag(Output i) : SV_Target
            {
                float2 uv = i.uv;
                float cloud = V5Noise(uv * float2(4, 2) + float2(_Time.y * .055, 2));
                float fade = max(0, 1 - length(2 * uv - 1));
                float streams = V5Noise(float2(uv.x * 19, uv.y * 5 - _Time.y * 3));
                float sheet = smoothstep(0,.17,uv.x) * (1-smoothstep(.83,1,uv.x)) * (.43 + streams*.57);
                return half4(i.color.rgb * lerp(1, .66 + streams*.75, _Pouring),
                    i.color.a * lerp(fade * fade * cloud, sheet, _Pouring));
            }
            ENDHLSL
        }
    }
}
