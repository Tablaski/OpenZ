// OpenZ-VK (GPL-3.0).
#ifndef OPENZ_AI_TERRAIN_V5
#define OPENZ_AI_TERRAIN_V5

float V5Hash(float2 p)
{
    float3 v = frac(p.xyx * float3(.1031, .1030, .0973));
    v += dot(v, v.yzx + 33.33);
    return frac((v.x + v.y) * v.z);
}

float V5Noise(float2 p)
{
    float2 cell = floor(p), t = frac(p);
    t = t * t * (3 - 2 * t);
    return lerp(lerp(V5Hash(cell), V5Hash(cell + float2(1, 0)), t.x),
        lerp(V5Hash(cell + float2(0, 1)), V5Hash(cell + 1), t.x), t.y);
}

void OZAiV5Surface(inout half3 color, float2 p, float pixelWidth, float biome, float road)
{
    float2 q = p + float2(V5Noise(p * .15), V5Noise(p.yx * .12 + 30)) * 5;
    float patches = V5Noise(q * .065) * .7 + V5Noise(p * .27) * .3;
    float fine = (V5Noise(p * 7.7) - .5) * (1 - smoothstep(.04, .17, pixelWidth));
    float plain = 1 - smoothstep(.25, .65, road);
    half3 original = color;
    if (biome < 1.5)
        color *= lerp(half3(1.075, 1.04, .99), half3(.67, .61, .56), smoothstep(.54, .81, patches));
    else if (biome < 2.5)
    {
        color *= 1 + .18 * fine - .24 * smoothstep(.51, .82, patches);
        float cluster = smoothstep(.77, .89, max(V5Noise(q * .23 + 3), V5Noise(q * .12)));
        color = lerp(color, lerp(half3(.19,.22,.057), half3(.29,.12,.17), step(.55, patches)),
            cluster * smoothstep(.01,.35,fine+.13) * .55);
    }
    else if (biome < 3.5)
    {
        float2 plate = p / 2.75;
        float2 seam = min(frac(plate), 1 - frac(plate));
        float joint = 1 - smoothstep(.008,.028 + pixelWidth*.24,min(seam.x,seam.y));
        float chip = smoothstep(.56,.78,V5Noise(p*1.05)) * smoothstep(.5,.79,patches);
        float checker = fmod(abs(floor(plate.x) + floor(plate.y)), 2);
        half3 tessera = lerp(half3(.31,.23,.18), half3(.33,.36,.28), checker);
        color *= 1 - joint*.21 - chip*.30;
        color = lerp(color,tessera,smoothstep(.70,.83,patches)*.37);
    }
    else if (biome < 4.5)
        color = lerp(color * (1 + fine * .20), color * half3(.71,.61,.47), smoothstep(.58,.85,patches));
    else
        color = lerp(color, color * half3(.83,.89,.98), smoothstep(.55,.78, patches + fine * .1));
    color = lerp(original, color, plain);
    if (biome > 4.5)
    {
        float windrow = smoothstep(.62,.8,V5Noise(q * float2(.85,.22)));
        float thin = smoothstep(.38,.54,V5Noise(p * 3));
        color = lerp(color,half3(.78,.86,.93),windrow*thin*road*(1-road*.49)*.42);
    }
}

#endif
