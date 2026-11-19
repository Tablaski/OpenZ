// The OzFxDist pass of the smoke / fire shaders (OpenZ/FxAdditive, OpenZ/FxBlast, OpenZ/FxLit).
// Why: the fog pass (Shaders/AtmosphereFullscreen.shader, a full-screen pass AFTER the transparents) knew only the opaque depth, and
// see-through smoke writes no depth: every smoke pixel took the haze of whatever stood BEHIND it (far rock: heavy haze, the sky beside it:
// the sky's haze and the horizon band), and the far land's edge cut a hard line through the smoke. This pass gives the fog pass the
// smoke's own distance: View/OzSmokeFog.cs draws every transparent material that has this pass, back to front, into one R16G16 float
// target per camera (cleared to 0) with Blend One OneMinusSrcAlpha:
//     R = eye depth x cover composited front over back, G = cover composited the same way (R / G = the visible smoke's mean eye depth)
// The fog pass then fogs the smoke's share (G) by that distance and only the rest by the distance behind it. Nothing else changes:
// a pixel without smoke (G = 0) is fogged exactly as before.
#ifndef OZ_FX_DIST_INCLUDED
#define OZ_FX_DIST_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

// c: the forward pass's own output (premultiplied rgb, alpha = how much of the scene behind it the smoke hides).
// glowK: the share of an additive glow (premultiplied rgb with little alpha: flashes, embers, flame cores) counted as cover (0 = alpha only,
// for straight-alpha materials whose rgb is a colour, not light).
half4 OzFxDistOut(half4 c, float4 positionCS, half glowK)
{
    float eye = LinearEyeDepth(positionCS.z, _ZBufferParams);                      // this fragment's eye depth (perspective cameras only: OzSmokeFog)
    float sceneEye = LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(positionCS)), _ZBufferParams);
    half glow = saturate(dot(max(c.rgb, (half3)0), half3(0.2126, 0.7152, 0.0722))) * glowK;
    half a = saturate(max(c.a, glow));
    a *= (half)step(eye, sceneEye + 0.05);                                          // hidden behind the opaque scene: no cover (the forward pass's own depth test)
    return half4((half)(eye * a), a, 0, a);
}
#endif
