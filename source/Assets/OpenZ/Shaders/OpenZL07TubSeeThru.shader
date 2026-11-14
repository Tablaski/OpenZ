// OpenZ (GPL-3.0) - version 1.3, level 7, the red side's win in the hot tub: the water a little see-through near the robots, so they read as sitting in water
// with their hips and legs under it, not cut off at the water line. A second draw of the tub's own water mesh, over the water as the game's lit surface drew it:
// what lies just under the surface shows through faintly, what lies deeper keeps the water's own colour.
//  - The water itself (its own material, unchanged) is drawn in the first slot after the opaque picture is taken (OzL07HotTubSet.Ending.cs), so that picture
//    and the depth texture hold what is under it. Both are on in every quality asset of the game (Depth Texture and Opaque Texture, no downsampling).
//  - Per pixel: the point behind the water (depth texture), how deep under the surface it lies (vertical, m), see = _SeeMost at the surface, gone by
//    _SeeDepth; and only near the robots: within _NearR of either body's middle (_NearA, _NearB, world, across the ground), faded over _NearSoft, so the
//    tub's walls and bench keep the water's own colour. What shows is that point's own colour from the opaque picture x _Tint, the water's hue. The foam
//    of the water's own picture stays on top.
//  - Nothing but the tub's water in that one ending uses it. The material Resources/OpenZ/L07Cut/M_L07_TubSeeThru keeps it in the build.
Shader "OpenZ/L07TubSeeThru"
{
    Properties
    {
        [NoScaleOffset] _BaseMap ("Water picture (its foam stays over what is under)", 2D) = "white" {}
        _WaterY ("Water surface, world y", Float) = 0
        _SeeMost ("See-through at the surface (0..1)", Range(0, 1)) = 0.5
        _SeeDepth ("Gone by this depth under the surface (m)", Float) = 0.55
        _NearA ("First body's middle (world)", Vector) = (0, 0, 0, 0)
        _NearB ("Second body's middle (world)", Vector) = (0, 0, 0, 0)
        _NearR ("Only within this of a body, across the ground (m; 0 = everywhere)", Float) = 0
        _NearSoft ("Faded over this (m)", Float) = 0.25
        _Tint ("What shows through, x this (linear)", Color) = (0.78, 0.93, 0.93, 1)
        _FoamKeep ("Foam kept over it (0..1)", Range(0, 1)) = 0.85
        _FoamLo ("Foam from this picture brightness (linear)", Float) = 0.15
        _FoamHi ("Full foam at this picture brightness (linear)", Float) = 0.45
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "SeeThru"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Back
            Offset -1, -1
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float _WaterY, _SeeMost, _SeeDepth, _FoamKeep, _FoamLo, _FoamHi, _NearR, _NearSoft;
                float4 _NearA, _NearB;
                half4 _Tint;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 suv = GetNormalizedScreenSpaceUV(i.positionCS);
            #if UNITY_REVERSED_Z
                float raw = SampleSceneDepth(suv);
            #else
                float raw = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, SampleSceneDepth(suv));
            #endif
                float3 bed = ComputeWorldSpacePosition(suv, raw, UNITY_MATRIX_I_VP);     // what this pixel of water lies over
                float under = _WaterY - bed.y;                                             // its depth under the surface, m
                half see = (half)(_SeeMost * (1.0 - smoothstep(0.0, max(_SeeDepth, 0.001), under)) * step(0.0, under));
                half foam = dot(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb, half3(0.2126, 0.7152, 0.0722));
                see *= (half)1.0 - (half)_FoamKeep * (half)smoothstep(_FoamLo, _FoamHi, foam);
                if (_NearR > 0.0)
                {   // near the robots only: the walls and the bench under the rest of the water stay hidden
                    float d = min(distance(bed.xz, _NearA.xz), distance(bed.xz, _NearB.xz));
                    see *= (half)(1.0 - smoothstep(max(_NearR - _NearSoft, 0.0), _NearR, d));
                }
                return half4(SampleSceneColor(suv) * _Tint.rgb, see);
            }
            ENDHLSL
        }
    }
}
