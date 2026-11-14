// OpenZ (GPL-3.0)
// and the far haze of OpenZ/AtmosphereFullscreen. The same glow is added on both sides of the horizon, so the glow under a low sun
// never ends in a hard line where the painted sky meets the hazed far ground (sky_panoramas_v1 README, note 2 / mockB).
// All globals are written by OpenZ.FlybySky (Scripts/View/FlybySky.cs). _FlybySkyW = 0 (gameplay) means: nothing changes.
#ifndef OPENZ_FLYBYSKY_INCLUDED
#define OPENZ_FLYBYSKY_INCLUDED

float _FlybySkyW;            // 0 = gameplay (the gradient skybox, no change anywhere), 1 = the flyby sky fully on (eased fade)
float4 _FlybySunDir;         // xyz = world direction TO the DayNight sun, w = sun strength (0 under the horizon / away from sunrise)
float4 _FlybySunGlow;        // rgb = glow colour (linear), a = strength of the wide glow
float4 _FlybySunShape;       // x = tight-glow exponent, y = wide-glow exponent, z = cos(disc radius), w = cos(disc radius + soft edge)
float4 _FlybySunDisc;        // rgb = disc colour (linear, HDR so the bloom catches it)
float _FlybySeaY;            // world height of the SKIRT / sea plane: sky pixels below the horizon get the haze at their distance to it
float _FlybyHorizonBlend;    // sin(horizonBlendDeg): the painting blends from the haze colour at the horizon into the painting above
float _FlybySkyClear;        // 0 = the sky is fully fogged as before .. 1 = the painting over the haze band
float _FlybyHazeBandK;       // the haze band's height as a multiple of _FlybyHorizonBlend
float _FlybySunThrough;      // share of the HDR disc that shows through the haze band

// 1.2 item 49 (the black world of PLAY AS BLUE): before the first level-start flyover of a process _FlybySunShape and _FlybySunDir
// were 0, pow(0, 0) is NaN on the GPU, and NaN x _FlybySkyW (0 in gameplay) is still NaN: the atmosphere pass turned every pixel
// black. The exponents are at least 1 (pow(0, e >= 1) = 0, never NaN) and c is at least 1e-4. With FlybySky's values (exponents
// 360 and 10) the exponent guard returns its input; the floor on c changes only directions where both glow terms are under 1.2e-40
// (below a float's smallest normal value: 0 in the picture), so the drawn glow is bit for bit as before.
half3 FlybyGlow(float3 d)
{
    float c = max(saturate(dot(d, _FlybySunDir.xyz)), 1e-4);
    float g = pow(c, max(_FlybySunShape.x, 1.0)) + _FlybySunGlow.a * pow(c, max(_FlybySunShape.y, 1.0));
    return (half3)(_FlybySunGlow.rgb * (g * _FlybySunDir.w));
}

// The sun disc mask on direction d (0..1, x the sun strength); the same shape as the dome draws.
float FlybySunDiscMask(float3 d)
{
    float c = dot(d, _FlybySunDir.xyz);
    // 1.2 item 49: never smoothstep(0, 0, c) (0 / 0) while the shape is not written yet. With FlybySky's shape the upper edge
    // cos 1.1 deg lies above cos 1.35 deg + 1e-6, so the edge is exactly _FlybySunShape.z as before
    return smoothstep(_FlybySunShape.w, max(_FlybySunShape.z, _FlybySunShape.w + 1e-6), c) * smoothstep(-0.002, 0.006, d.y) * _FlybySunDir.w;
}

// 1.2 item 49: normalize(v), or the fallback direction where v has no length (a flyby global not written yet): never normalize(0) =
// NaN. Wherever v has a length it is exactly normalize(v) (the same call on the same value).
float3 FlybySafeDir(float3 v, float3 fallback)
{
    return normalize(dot(v, v) > 1e-12 ? v : fallback);
}

#endif
