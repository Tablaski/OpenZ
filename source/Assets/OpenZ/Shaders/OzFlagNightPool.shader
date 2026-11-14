// OpenZ/FlagNightPool: the neutral flag's pool of light at night (version 1.2, owner item 5b, idea 06; View/FlagView.Looks.cs): a
// soft round spot of cool light on the ground round the pole that slowly breathes, drawn the way the street lamps' pools are
// (OpenZ/NightPool): every scene pixel inside a box over the spot is rebuilt from the camera depth texture and lit, so the pool
// lies on sand, roads, kerbs and craters and on the robots fighting at the flag; the light comes from above (faces turned away
// keep _WallLight) and falls off with the height above the ground under the pole. No real light, no shadow.
// One mesh for every zone flag, a box per flag; per vertex: COLOR = light colour (linear) + a = the flag's pool amount (faded by
// the view on captures; 0 = its box collapses, no pixel runs), uv0 = (pole x, ground y under the pole, pole z, radius m),
// uv1 = (phase seed in radians, band below m, band above m, 0); x the flags' night (_OZFlagLook.x, OzFlagLamp.hlsl).
//   "PoolLight" (UniversalForward): Blend DstColor One -> dst * (1 + light): albedo-correct brightening
//   "PoolGlow"  (SRPDefaultUnlit):  Blend One One      -> a faint added glow, so black ground still shows the pool
Shader "OpenZ/FlagNightPool"
{
    Properties
    {
        _Gain ("Light gain (the ground x 1 + gain at the middle)", Range(0, 8)) = 1.3
        _Softness ("Edge softness", Range(0.05, 1)) = 0.6
        _GlowShare ("Added glow share", Range(0, 0.5)) = 0.12
        _WallLight ("Light kept on faces turned away", Range(0, 1)) = 0.35
        _DropFade ("Falloff with the height above the ground in m", Range(0.2, 6)) = 2.5
        _Breath ("Breathing depth", Range(0, 0.5)) = 0.18
        _BreathPeriod ("Breathing period in s", Float) = 4.6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-20" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "OzFlagLamp.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float _Gain;
            float _Softness;
            float _GlowShare;
            float _WallLight;
            float _DropFade;
            float _Breath;
            float _BreathPeriod;
        CBUFFER_END

        struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float4 uv0 : TEXCOORD0; float4 uv1 : TEXCOORD1; };
        struct Varyings { float4 positionCS : SV_POSITION; float4 color : TEXCOORD0; float4 pool : TEXCOORD1; float4 band : TEXCOORD2; };

        Varyings PoolVert(Attributes v)
        {
            Varyings o;
            float amount = v.color.a * saturate(_OZFlagLook.x);
            o.positionCS = amount > 0.002 ? TransformObjectToHClip(v.positionOS.xyz) : float4(-2.0, -2.0, -2.0, 1.0);
            o.color = float4(v.color.rgb, amount);
            o.pool = v.uv0; o.band = v.uv1;
            return o;
        }

        // light share (0..1) at the scene pixel behind this fragment
        float PoolAt(Varyings i)
        {
            float2 suv = GetNormalizedScreenSpaceUV(i.positionCS);
            float raw = SampleSceneDepth(suv);
            float3 wp = ComputeWorldSpacePosition(suv, raw, UNITY_MATRIX_I_VP);
            float3 n = normalize(cross(ddy(wp), ddx(wp)) + float3(0, 1e-7, 0));   // derivatives before any early return
            #if UNITY_REVERSED_Z
                if (raw <= 1e-6) return 0;
            #else
                if (raw >= 1.0 - 1e-6) return 0;
            #endif
            float h = wp.y - i.pool.y;                                              // height above the ground under the pole
            if (h < -i.band.y || h > i.band.z) return 0;
            float r = length(wp.xz - i.pool.xz) / max(i.pool.w, 0.1);              // 0 at the pole .. 1 at the pool's edge
            float fall = saturate((1.0 - r) / max(_Softness, 1e-3));
            fall = fall * fall * (3.0 - 2.0 * fall);
            float band = (1.0 - smoothstep(i.band.z * 0.6, i.band.z, h)) * (1.0 - smoothstep(i.band.y * 0.6, i.band.y, -h));
            if (dot(n, _WorldSpaceCameraPos - wp) < 0) n = -n;
            float up = saturate(n.y);
            float facing = lerp(_WallLight, 1.0, up * up);
            float dh = h / max(_DropFade, 1e-3);
            float drop = 1.0 / (1.0 + dh * dh);
            float breathe = 1.0 - _Breath + _Breath * (0.5 + 0.5 * sin(_Time.y * 6.2831853 / max(_BreathPeriod, 0.1) + i.band.x));
            return fall * band * facing * drop * breathe * i.color.a;
        }
        ENDHLSL

        Pass
        {
            Name "PoolLight"
            Tags { "LightMode"="UniversalForward" }
            Blend DstColor One
            ZWrite Off
            ZTest Always
            Cull Front
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PoolVert
            #pragma fragment PoolLightFrag
            half4 PoolLightFrag(Varyings i) : SV_Target
            {
                float k = PoolAt(i);
                return half4(i.color.rgb * (_Gain * k), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "PoolGlow"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Front
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PoolVert
            #pragma fragment PoolGlowFrag
            half4 PoolGlowFrag(Varyings i) : SV_Target
            {
                float k = PoolAt(i);
                return half4(i.color.rgb * (_Gain * _GlowShare * k), 0.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
