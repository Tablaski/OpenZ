// Level 7, kit LV2: what a burst leaves on the lava (View/OzLavaDomes.cs draws it, instanced, 48 at most, on a flat quad of half size = the radius).
// Per instance _OZRing: x = age s, y = run of the ring s, z = healing of the scar s (0 = no scar), w = strength. A flash for 0.12 s, a broken
// ring running outward and cooling down the one ramp (OzLavaRamp.hlsl), then a dark scar of crust that heals. Premultiplied: light adds, the
// scar covers. Replaces the 8-slot loop every lava pixel used to run. Replaces nothing else; no texture.
Shader "OpenZ/LavaRing"
{
    Properties
    {
        _ScarDark ("Scar cover", Range(0, 1)) = 0.72
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-5" "IgnoreProjector"="True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Back
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "OzLavaRamp.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _ScarDark;
            CBUFFER_END
            UNITY_INSTANCING_BUFFER_START(OzLavaRingProps)
                UNITY_DEFINE_INSTANCED_PROP(float4, _OZRing)
            UNITY_INSTANCING_BUFFER_END(OzLavaRingProps)
            half _OZNight;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float seed : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                float3 c = TransformObjectToWorld(float3(0, 0, 0));
                o.seed = frac((c.x + c.z * 1.7) * 0.37);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 d = UNITY_ACCESS_INSTANCED_PROP(OzLavaRingProps, _OZRing);
                float age = d.x, life = d.y, heal = d.z, k = d.w;
                float r = length(i.uv), ang = atan2(i.uv.y, i.uv.x + 1e-5), s = i.seed * 6.2832;      // atan2(0, 0) can be NaN, and one NaN pixel is a black square in the bloom
                float edge = 1.0 - smoothstep(0.9, 1.0, r);                                           // the wobble must never show the quad's edge
                float wob = 1.0 + 0.09 * sin(ang * 3.0 + s) + 0.05 * sin(ang * 7.0 + s * 2.0) + 0.03 * sin(ang * 13.0 + s * 3.0);
                float rr = r * wob;
                float u = saturate(age / life);
                float front = 0.22 + 0.72 * (1.0 - (1.0 - u) * (1.0 - u));
                float width = 0.05 + 0.09 * u;
                float band = (1.0 - smoothstep(0.0, width, abs(rr - front))) * (0.7 + 0.3 * sin(ang * 5.0 - s)) * (1.0 - u * u) * edge;
                half T = (half)(lerp(1.08, 0.5, u) * band * k);
                half3 light = OzLavaEmit(T, _OZNight);
                float flash = saturate(1.0 - age / 0.12) * (1.0 - smoothstep(0.0, 0.7, r)) * max(k * 2.0 - 1.0, 0.0);   // k 0.45 = CALM SCREEN: no flash at all
                light += OzLavaRamp((half)1.25) * (half)flash;
                float a = 0.0;
                if (heal > 0.0)
                {
                    float grow = smoothstep(0.05, 0.4, u), left = 1.0 - saturate((age - life) / heal);
                    a = grow * left * left * (1.0 - smoothstep(front * 0.55, front * 0.98, rr)) * _ScarDark * edge;
                }
                Light L = GetMainLight();
                half3 crust = (half3)_OZLavaRamp[0].rgb * (0.35 + L.color * (half)saturate(L.direction.y));
                return half4(light + crust * (half)a, (half)a);
            }
            ENDHLSL
        }
    }
}
