// reflection comes from the MIRROR PICTURE of the level (View/OzWaterMirror.cs: the things above the water drawn once more, seen through
// the water's surface as a mirror, half the screen's size, solid things only). Each water point reads the picture at its own place in it
// (_OZWaterMirrorVP), so every pixel of a reflection is the true mirror image of the thing standing there: nothing that is not above that
// water can appear on it (round 5's screen-space ray picked up the quay tops as brown sheets). What is in the picture is told from its
// depth (a texel nothing was drawn on is sky: the water keeps its sky reflection there), four texels round the point give a soft edge.
// Where there is no mirror picture (_OZWaterMirror.x = 0: briefings, flyovers, the menu, a camera that is not the player's; a pool at
// another height than the picture's) the function is the screen-space one of kit water_tune_r3, line for line (OzWaterObjReflectSS).
// Global _OZWaterObjRefl (View/OzWaterPlayCalm.cs): x = strength of the screen-space reflection (0 = off, unset = 0 = the water exactly as
// before), y = how much of the ripples bend it. Skipped on GRAPHICS QUALITY LOW (_OZWaterLite.w = 1, View/OzLitePresets.cs "water glints").
// Globals of the mirror picture (View/OzWaterMirror.cs): _OZWaterMirror x = 1 while this camera has a picture, y = the water height it was
// made for (m), z = its strength, w = how far the ripples push it (m per unit of slope); _OZWaterMirrorVP = world -> the picture;
// _OZWaterMirrorTex / _OZWaterMirrorDepth = its colour / depth; _OZWaterMirrorTx xy = one texel, z = 1 where the picture is stored top-down.
// picture moves to another water height): 1 = the picture alone (as before), 0 = the screen-space reflection alone, between = both mixed.
#ifndef OZ_WATER_OBJ_REFLECT_INCLUDED
#define OZ_WATER_OBJ_REFLECT_INCLUDED

float4 _OZWaterObjRefl;
float4 _OZWaterMirror;
float4 _OZWaterMirrorTx;
float4x4 _OZWaterMirrorVP;
TEXTURE2D(_OZWaterMirrorTex);
TEXTURE2D_FLOAT(_OZWaterMirrorDepth);

float2 OzObjReflUV(float3 p, float flip, out float eye)
{
    float4 c = TransformWorldToHClip(p);
    eye = -TransformWorldToView(p).z;
    float2 uv = c.xy / max(c.w, 1e-4) * 0.5 + 0.5;
    uv.y = lerp(uv.y, 1.0 - uv.y, flip);
    return uv;
}

// kit water_tune_r3's screen-space reflection, every line as it was (only its name is new). rgb = the reflected scene colour, a = its weight
half4 OzWaterObjReflectSS(float3 positionWS, half3 n, half3 V, float2 screenUV)
{
    if (_OZWaterObjRefl.x <= 0.0 || !IsPerspectiveProjection()) return half4(0.0, 0.0, 0.0, 0.0);
    const half3 up = half3(0.0, 1.0, 0.0);
    half3 nR = normalize(lerp(up, n, (half)_OZWaterObjRefl.y));
    float3 R = reflect(-(float3)V, (float3)nR);
    if (R.y < 0.03) return half4(0.0, 0.0, 0.0, 0.0);
    // which way up the screen is in this target (game view, render texture): the surface point's own UV decides
    float e0;
    float2 u0 = OzObjReflUV(positionWS, 0.0, e0);
    float flip = abs((1.0 - u0.y) - screenUV.y) < abs(u0.y - screenUV.y) ? 1.0 : 0.0;
    float tPrev = 0.0, tHit = -1.0, tCur = 0.3;
    [loop] for (int i = 0; i < 14; i++)
    {
        float eye;
        float2 uv = OzObjReflUV(positionWS + R * tCur, flip, eye);
        if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0 || eye < 0.05) break;
        float sceneEye = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
        float dz = eye - sceneEye;
        if (dz > 0.02 && dz < 0.5 + tCur * 0.3) { tHit = tCur; break; }        // behind a surface, within its thickness
        tPrev = tCur; tCur = tCur * 1.38 + 0.3;                                  // 0.3 m .. ~40 m along the ray
    }
    if (tHit < 0.0) return half4(0.0, 0.0, 0.0, 0.0);
    float a = tPrev, b = tHit;
    [unroll] for (int j = 0; j < 3; j++)
    {
        float m = 0.5 * (a + b), em;
        float2 um = OzObjReflUV(positionWS + R * m, flip, em);
        if (em - LinearEyeDepth(SampleSceneDepth(um), _ZBufferParams) > 0.02) b = m; else a = m;
    }
    float eb;
    float2 uvH = OzObjReflUV(positionWS + R * b, flip, eb);
    half3 col = SAMPLE_TEXTURE2D_X_LOD(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uvH, 0).rgb;   // after a branchy loop: no gradients
    float2 ed = min(uvH, 1.0 - uvH);
    half w = (half)(smoothstep(0.0, 0.07, ed.x) * smoothstep(0.0, 0.07, ed.y));   // no hard cut at the screen edge
    w *= (half)(1.0 - smoothstep(24.0, 40.0, b));                                    // far hits fade out
    return half4(col, w);
}

// kit object_reflections_r6: the mirror picture at this water point. rgb = the mirrored thing's colour, a = its weight (0 = sky there)
half4 OzWaterMirrorRead(float3 positionWS, half3 n)
{
    float3 p = positionWS;
    p.xz += (float2)n.xz * _OZWaterMirror.w;                                       // the ripples push the image about, in metres on the water
    float4 c = mul(_OZWaterMirrorVP, float4(p, 1.0));
    if (c.w < 0.05) return half4(0.0, 0.0, 0.0, 0.0);
    float2 uv = c.xy / c.w * 0.5 + 0.5;
    if (uv.x <= 0.0 || uv.x >= 1.0 || uv.y <= 0.0 || uv.y >= 1.0) return half4(0.0, 0.0, 0.0, 0.0);
    half top = (half)smoothstep(0.0, 0.05, 1.0 - uv.y);                             // a thing above the screen's top edge is not in the picture: no hard start there
    uv.y = lerp(uv.y, 1.0 - uv.y, _OZWaterMirrorTx.z);
    float2 h = _OZWaterMirrorTx.xy * 0.5;
    half3 col = half3(0.0, 0.0, 0.0); half cov = 0.0;
    [unroll] for (int i = 0; i < 4; i++)
    {
        float2 u = uv + h * float2((i & 1) ? 1.0 : -1.0, (i & 2) ? 1.0 : -1.0);
        float d = SAMPLE_TEXTURE2D_LOD(_OZWaterMirrorDepth, sampler_PointClamp, u, 0).r;
#if UNITY_REVERSED_Z
        half k = d > 0.0 ? (half)1.0 : (half)0.0;                                   // something was drawn on this texel
#else
        half k = d < 1.0 ? (half)1.0 : (half)0.0;
#endif
        col += SAMPLE_TEXTURE2D_LOD(_OZWaterMirrorTex, sampler_PointClamp, u, 0).rgb * k; cov += k;
    }
    if (cov < 0.5) return half4(0.0, 0.0, 0.0, 0.0);
    col = col / cov;
    half mx = max(col.r, max(col.g, col.b));
    col *= min((half)1.0, (half)0.85 / max(mx, (half)1e-4));                        // round 6 Unity pass B (14 Nov, A15 / A16 family): a lit lamp head or a fire in the picture keeps its colour but stays under the bloom line (was min 4.0: at night every lamp head stood on the water as a glowing crescent ring)
    half w = cov * (half)0.25 * top * (half)(_OZWaterMirror.z / max(_OZWaterObjRefl.x, 1e-3));   // the water shaders mix by a * _OZWaterObjRefl.x
    return half4(col, w);
}

// rgb = the reflected colour, a = its weight (0 = nothing there: keep the sky reflection)
half4 OzWaterObjReflect(float3 positionWS, half3 n, half3 V, float2 screenUV)
{
    if (_OZWaterObjRefl.x <= 0.0) return half4(0.0, 0.0, 0.0, 0.0);
    // the mirror picture is for ONE water height: a pool at another height keeps the screen-space reflection (never a shifted image)
    half k = abs(positionWS.y - _OZWaterMirror.y) < 0.06 ? (half)saturate(_OZWaterMirror.x) : (half)0.0;
    if (k > (half)0.999) return OzWaterMirrorRead(positionWS, n);                         // the share full: the picture alone (as before)
    half4 ss = OzWaterObjReflectSS(positionWS, n, V, screenUV);
    if (k < (half)0.001) return ss;                                                        // no share: the screen-space one alone (as before)
    half4 mr = OzWaterMirrorRead(positionWS, n);
    half a = lerp(ss.a, mr.a, k);                                                          // the water shaders mix by a: the two weights mixed ...
    half3 c = (ss.rgb * ss.a * ((half)1.0 - k) + mr.rgb * mr.a * k) / max(a, (half)1e-4);  // ... and the two colours by their weights
    return half4(c, a);
}

#endif
