// OpenZ-VK (GPL-3.0).
#ifndef OPENZ_AI_TERRAIN_V4
#define OPENZ_AI_TERRAIN_V4

float OZAiV4Hash(float2 cell)
{
    float3 p = frac(float3(cell.xyx) * float3(.1031, .1030, .0973));
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

// Scattered elliptical grains lose contrast as they approach a pixel in size.
float OZAiV4Grains(float2 uv, float pixelWidth, float density)
{
    float seed = OZAiV4Hash(floor(uv));
    float2 shift = float2(seed, frac(seed * 13.37)) * .4 - .2;
    float2 p = (frac(uv) - .5 - shift) * float2(1, 1.35);
    float radius = lerp(.12, .27, seed);
    float gradient = max(pixelWidth, .025);
    float grain = 1 - smoothstep(radius - gradient, radius + gradient, length(p));
    return grain * step(seed, density) * (1 - smoothstep(.18, .46, pixelWidth));
}

void OZAiV4Surface(inout half3 albedo, float2 worldXZ, float pixelWidth, float profile, float roadWeight)
{
    if (profile < .5 || pixelWidth > .38) return;
    float2 crossUV = float2(dot(worldXZ, float2(.8, -.6)), dot(worldXZ, float2(.6, .8)));
    float fine = OZAiV4Grains(worldXZ * 22, pixelWidth * 22, .85);
    float coarse = OZAiV4Grains(crossUV * 6, pixelWidth * 6, .72);
    float scale = 3.1;
    float strength = .11;
    float form = 0;
    if (profile < 1.5)                 // L02 sandstone and windblown sand.
    {
        float waviness = sin(crossUV.x * 1.7) * .42 + sin(crossUV.y * .9) * .23;
        form = sin(crossUV.y * 31 + waviness * 5) * .075 * (1 - smoothstep(.028, .105, pixelWidth));
    }
    else if (profile < 2.5)            // L07 ash and scattered scoria.
    {
        scale = 4.5;
        strength = .19;
    }
    else if (profile < 3.5)            // L17 concrete pits, finer than the painted joints.
    {
        scale = 8.0;
        strength = .20;
    }
    else if (profile < 4.5)            // L16 moss, damp earth and sparse leaf litter.
    {
        scale = 3.8;
        strength = .22;
    }
    else                              // Wind-packed Arctic snow and exposed gravel.
    {
        scale = 10;
        strength = .09;
        form = sin(crossUV.y * 18 + sin(crossUV.x * .9) * 1.4) * .028 *
            (1 - smoothstep(.035, .13, pixelWidth));
    }
    float aggregate = OZAiV4Grains(crossUV * scale, pixelWidth * scale, .53);
    float earth = fine * .16 - aggregate * strength + form;
    float asphalt = fine * .31 + coarse * .13
        - OZAiV4Grains(worldXZ.yx * 11 + 19, pixelWidth * 11, .65) * .15;
    float weight = smoothstep(.28, .72, roadWeight);
    albedo *= 1 + lerp(earth, asphalt, weight);
}

#endif
