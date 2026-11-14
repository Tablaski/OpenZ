// OpenZ/EvilZodGhost: the smear of his power dash. Additive ice blue, bright on the silhouette's edge and clear in the middle, no depth write, HDR over the bloom threshold.
// Drawn from meshes baked off his body (View/OzEvilZodFx.cs). _Alpha comes per ghost from a property block.
Shader "OpenZ/EvilZodGhost"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (0.5, 1.0, 2.6, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.22
        _Power ("Edge power", Range(0.5, 6)) = 2.2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+15" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color; half _Alpha; half _Power;
            CBUFFER_END
            struct A { float4 pos : POSITION; float3 nrm : NORMAL; };
            struct V { float4 pos : SV_POSITION; float3 nrm : TEXCOORD0; float3 view : TEXCOORD1; };
            V vert(A i)
            {
                V o; float3 w = TransformObjectToWorld(i.pos.xyz);
                o.pos = TransformWorldToHClip(w); o.nrm = TransformObjectToWorldNormal(i.nrm); o.view = GetWorldSpaceViewDir(w);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                half f = 1 - saturate(dot(normalize(i.nrm), normalize(i.view)));
                return half4(_Color.rgb * (0.25 + pow(f, _Power)) * _Alpha, 0);
            }
            ENDHLSL
        }
    }
}
