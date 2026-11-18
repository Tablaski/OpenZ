Shader "Hidden/OpenZ/RadarGroundFog"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment RadarGroundFog
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_RadarSight);
            float4 _RadarMapScale;
            float _RadarSignalStrength;
            float4 _RadarInterference;

            float RadarNoise(float2 p)
            {
                p = frac(p * float2(0.1031, 0.11369));
                p += dot(p, p.yx + 33.33);
                return frac((p.x + p.y) * p.x);
            }

            half4 RadarGroundFog(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float depth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (depth <= 0.000001) return original;
                #else
                    if (depth >= 0.999999) return original;
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                #endif
                float3 position = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float2 map = position.xz * _RadarMapScale.xy;
                if (any(map < 0) || any(map > 1)) return original;
                half clear = SAMPLE_TEXTURE2D(_RadarSight, sampler_LinearClamp, map).r;
                half affected = (1 - smoothstep(0.15, 0.9, clear)) * _RadarSignalStrength;
                if (affected < 0.001) return original;

                float seconds = _RadarInterference.x;
                float cycle = floor(seconds / 4.75);
                float age = fmod(seconds, 4.75) - lerp(0.45, 2.5, RadarNoise(float2(cycle, 6.3)));
                float burst = smoothstep(0, 0.045, age) * (1 - smoothstep(0.17, 0.34, age)) * _RadarInterference.y;
                half3 received = original.rgb;
                half staticNoise = 0;
                if (burst > 0.001)
                {
                    float tick = floor(seconds * 32);
                    float band = floor(uv.y * 146);
                    float noise = RadarNoise(float2(band, tick));
                    float shift = (noise - 0.5) * step(0.67, noise) * 0.004 * burst;
                    received = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(shift, 0)).rgb;
                    staticNoise = (RadarNoise(floor(input.positionCS.xy * 0.68) + tick) - 0.5) * 0.075 * burst;
                }
                half luminance = dot(received, half3(0.2126, 0.7152, 0.0722)) * 0.82 + 0.025 + staticNoise;
                return half4(lerp(original.rgb, max(0, luminance).xxx, affected), original.a);
            }
            ENDHLSL
        }
    }
}
