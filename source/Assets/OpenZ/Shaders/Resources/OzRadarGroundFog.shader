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
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment RadarGroundFog
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_RadarSight);
            float4 _RadarMapScale;
            float _RadarFogOpacity;

            half4 RadarGroundFog(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float depth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (depth <= 0.000001) return 0;
                #else
                    if (depth >= 0.999999) return 0;
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                #endif
                float3 position = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float2 map = position.xz * _RadarMapScale.xy;
                if (any(map < 0) || any(map > 1)) return 0;
                half clear = SAMPLE_TEXTURE2D(_RadarSight, sampler_LinearClamp, map).r;
                return half4(0.18, 0.19, 0.20, (1 - smoothstep(0.12, 0.9, clear)) * _RadarFogOpacity);
            }
            ENDHLSL
        }
    }
}
