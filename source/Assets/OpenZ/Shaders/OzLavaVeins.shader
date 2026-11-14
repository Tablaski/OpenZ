// OpenZ (GPL-3.0) - version 1.3, level 7: the thin glowing cracks on dry ground (LAV_Veins). A low additive ribbon, lifted by a depth offset so it never z-fights
// the ground. Global _OZLavaMood is the lava's live heat (0 or unset = steady 1), _OZNight the night value every lit material reads.
Shader "OpenZ/LavaVeins"
{
    Properties
    {
        _BaseMap ("Strip", 2D) = "black" {}
        [HDR] _Tint ("Tint", Color) = (2.2, 2.2, 2.2, 1)
        _NightGain ("Extra at night", Float) = 0.15
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-20" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Veins"
            Blend One One
            ZWrite Off
            Cull Off
            Offset -2, -2
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; half4 _Tint; half _NightGain;
            CBUFFER_END
            half _OZNight; float _OZLavaMood;
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fade : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv; o.fade = i.color.r;
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                half3 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                float heat = _OZLavaMood > 0.01 ? _OZLavaMood : 1.0;
                return half4(c * _Tint.rgb * i.fade * (half)heat * (1 + _NightGain * _OZNight), 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
