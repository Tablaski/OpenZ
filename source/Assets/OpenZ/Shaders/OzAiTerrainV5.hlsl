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

float V5Resolved(float metresPerPixel, float frequency)
{
    return 1 - smoothstep(.22, .62, metresPerPixel * frequency);
}

// Voronoi cell boundaries form ice fractures and the angular faces of broken stone.
float2 V5Cells(float2 p)
{
    float2 cell = floor(p), local = frac(p);
    float nearest = 9, second = 9;
    [unroll] for (int y = -1; y <= 1; ++y)
    [unroll] for (int x = -1; x <= 1; ++x)
    {
        float2 offset = float2(x, y);
        float2 id = cell + offset;
        float2 seedPosition = .13 + .74 * float2(V5Hash(id), V5Hash(id + 43));
        float2 delta = offset + seedPosition - local;
        float d = dot(delta, delta);
        second = min(second, max(d, nearest));
        nearest = min(nearest, d);
    }
    return float2(sqrt(nearest), sqrt(second) - sqrt(nearest));
}

float3 V5Normal(float3 normalWS, float3 positionWS, float height)
{
    float3 dx = ddx(positionWS), dy = ddy(positionWS);
    float3 alongX = cross(dy, normalWS), alongY = cross(normalWS, dx);
    float determinant = dot(dx, alongX);
    float3 gradient = alongX * ddx(height) + alongY * ddy(height);
    return normalize(normalWS - gradient * (sign(determinant) / max(abs(determinant), 1e-8)));
}

float V5LakeIceMask(float palette)
{
    return smoothstep(.20,.75,palette);
}

float OZAiV5Surface(inout half3 color, float2 p, float pixel, float biome, float road, float palette)
{
    float coarse = V5Noise(p * .21 + 11);
    float medium = V5Noise(p * 2.6) - .5;
    float grit = (V5Noise(p * 13.0) - .5) * V5Resolved(pixel, 13);
    float roadWeight = smoothstep(.25, .65, road);
    float elevation = .013 * medium * V5Resolved(pixel, 2.6) + .007 * grit;
    half3 terrain = color;
    if (biome < 1.5)
    {
        float2 wind = float2(dot(p, float2(.88,.47)), dot(p, float2(-.47,.88)));
        float sheet = V5Noise(float2(wind.x * .10, wind.y * .19 + coarse * .55));
        float streak = smoothstep(.46,.78,sheet) * (.32 + .68 * palette);
        float duneField = smoothstep(.43,.74,V5Noise(wind * float2(.22,.13) + 19));
        float ripple = sin(wind.y * 13 + V5Noise(wind * .24) * 8 + coarse * 2.5);
        ripple *= V5Resolved(pixel, 2.2) * duneField;
        terrain *= lerp(half3(1.025,1.012,1),half3(.72,.65,.57),streak) * (1 + ripple * .053 + grit * .24);
        elevation += ripple * .005;
    }
    else if (biome < 2.5)
    {
        float scoria = smoothstep(.43,.70,V5Noise(p * .53)) * (.52 + .48 * coarse);
        float2 stone = V5Cells(p * 2.8 + medium * .1);
        float grains = smoothstep(.43,.56,stone.x) * V5Resolved(pixel, 2.8);
        float cleft = (1 - smoothstep(.025,.13,stone.y)) * V5Resolved(pixel, 2.8);
        terrain *= lerp(half3(1.10,1.10,1.11),half3(.63,.63,.67),scoria) * (1 - .18 * grains - .15 * cleft + grit * .37);
        elevation += (.025 * grains - .020 * cleft) * scoria;
    }
    else if (biome < 3.5)
    {
        float nativeZ = -p.y;
        float2 slab = float2((p.x + floor(nativeZ / 4.7) * 3.3) / 6.6, nativeZ / 4.7);
        float2 gap = abs(frac(slab + .5) - .5) * float2(6.6, 4.7);
        float seam = min(gap.x, gap.y);
        float chip = smoothstep(.53,.76,V5Noise(p * 1.25)) * .19;
        float joint = 1 - smoothstep(.028 + chip,.085 + chip + pixel,seam);
        float bevel = 1 - smoothstep(.055 + chip,.21 + chip + pixel,seam);
        float unitTone = V5Hash(floor(slab)) - .5;
        float inlay = smoothstep(.60,.86,palette) * smoothstep(.27,.55,coarse);
        half3 warmPaving = lerp(half3(.88,.73,.62),half3(.84,.92,.90),step(0,unitTone));
        terrain *= lerp((half3)1,warmPaving,inlay * .62) * (1 + unitTone*.11 - joint*.21 + (bevel-joint)*.10 + grit*.16);
        elevation -= joint * .028 + bevel * .010;
    }
    else if (biome < 4.5)
    {
        float mud = palette * smoothstep(.38,.70,coarse + medium * .19);
        float baked = (V5Noise(p * .75) - .5) * .22;
        terrain *= lerp(half3(1.02,1.02,.99),half3(.81,.73,.62),mud) * (1 + baked + grit*.26);
        elevation *= 1 + mud * .42;
    }
    else
    {
        float dune = V5Noise(p * float2(.21,.73) + coarse * .9);
        float drift = smoothstep(.55,.82,dune);
        float glaze = V5LakeIceMask(palette);
        float2 crystal = V5Cells(p * float2(.38,.54) + V5Noise(p * .86) * .12);
        float cracks = (1 - smoothstep(.003,.018,crystal.y)) * smoothstep(.37,.69,V5Noise(p * .63));
        float blue = V5Noise(p * .19) * .76 + V5Noise(p * .92) * .24;
        half3 ice = lerp(half3(.034,.123,.184),half3(.115,.266,.339),blue);
        ice += half3(.14,.21,.25) * (cracks * .49 + smoothstep(.64,.84,V5Noise(p * 1.13)) * .19);
        terrain = lerp(terrain * (1 + drift * .071 + grit * .15), ice, glaze * .94);
        elevation = lerp(elevation + drift * .017, -cracks * .0025,glaze);
        float driftOnRoad = smoothstep(.63,.82,dune + medium * .12) * (.30 + .53 * (1 - road));
        float gravel = (V5Noise(p * 8.6) - .5) * V5Resolved(pixel, 8.6);
        color = lerp(color * (1 + gravel * .30),half3(.65,.73,.78),driftOnRoad);
    }
    float asphalt = (V5Noise(p * 5.8) - .5) * V5Resolved(pixel, 5.8);
    color = lerp(terrain,color * (1 + grit*.28 + asphalt*.16),roadWeight);
    return lerp(elevation, asphalt * (biome > 4.5 ? .024 : .012) + grit * .006,roadWeight);
}

float V5Rock(inout half3 albedo, float3 p, float3 normalWS, float biome, float pixel)
{
    float2 wall = abs(normalWS.x) > abs(normalWS.z) ? p.zy : p.xy;
    float top = smoothstep(.40,.81,abs(normalWS.y));
    float cells = V5Noise(wall * float2(3.2,1.3));
    float bedding = V5Noise(wall * float2(.42,3.7) + V5Noise(wall*.51) * .85);
    float pebbles = V5Noise(p.xz * 3.1);
    float cracks = smoothstep(.56,.76,lerp(cells,pebbles,top)) * V5Resolved(pixel, 3.2);
    float layers = (bedding - .5) * (1 - top) * V5Resolved(pixel, 3.7);
    float warmth = V5Noise(p.xz * .25 + p.y * .13);
    half3 tint = biome < 1.5 ? half3(.77,.67,.57) : biome > 4.5 ? half3(.65,.76,.87) : half3(.74,.71,.70);
    albedo *= lerp((half3)1,tint,cracks * .62) * (1 + layers*.31 + (warmth-.5)*.15);
    if (biome < 1.5)
    {
        half mineral = dot(albedo, half3(.30,.59,.11));
        albedo = lerp(albedo,mineral * half3(1.12,1.03,.87),.22 + top*.16);
    }
    if (biome > 4.5)
    {
        float packed = V5Noise(p.xz * float2(.52,.93) + warmth * .69);
        float coat = smoothstep(.42,.90,normalWS.y + (packed-.39)*.34);
        half3 snow = lerp(half3(.54,.65,.72),half3(.75,.83,.87),packed);
        albedo = lerp(albedo,snow * (1 + (pebbles-.5)*.10),coat*.83);
        return lerp(layers*.031 - cracks*.021,(pebbles-.5)*.010,coat);
    }
    return layers * .031 - cracks * .021;
}

#endif
