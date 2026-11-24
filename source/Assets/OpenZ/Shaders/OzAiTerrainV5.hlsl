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
    return smoothstep(.30,.67,palette);
}

// Keep a metre-scale signal when individual grains become smaller than a pixel.
float V5SurfaceGrain(float2 p, float pixel)
{
    return (V5Noise(p * 1.7) - .5) * .25 * V5Resolved(pixel,1.7)
        + (V5Noise(p * 5.2 + 19) - .5) * .29 * V5Resolved(pixel,5.2)
        + (V5Noise(p * 14.5 - 7) - .5) * .38 * V5Resolved(pixel,14.5);
}

float V5ArcticSurface(inout half3 color, float2 p, float pixel, float road, float iceMask)
{
    float roadWeight = smoothstep(.31,.67,road);
    float2 wind = float2(dot(p,float2(.82,.57)),dot(p,float2(-.57,.82)));
    float drift = V5Noise(p * .46) * .66 + V5Noise(p * 1.08 + 8) * .34;
    float2 warp = float2(V5Noise(p * 2.3 + 8),V5Noise(p * 2.6 - 17)) - .5;
    float2 crustCells = V5Cells(p * 8.2 + warp * .56);
    float crust = smoothstep(.10,.50,crustCells.x) * V5Resolved(pixel,8.2);
    float grains = (V5Noise(wind * 24.0 + warp) - .5) * V5Resolved(pixel,24);
    float snowbank = smoothstep(.57,.83,V5Noise(wind * float2(.31,.95)) * .77 + V5Noise(p * .68) * .23);
    float gravel = V5SurfaceGrain(p + 61,pixel);
    half3 snow = color * (.96 + (drift-.5)*.095 + (.45-crust)*.095 + grains*.12);
    half3 exposedRoad = color * (.96 + gravel*1.27);
    float dust = snowbank * lerp(.73,.39,road) * roadWeight;
    color = lerp(snow,exposedRoad,roadWeight);
    color = lerp(color,half3(.50,.56,.61) * (1 + gravel*.53 + (.45-crust)*.08),dust);

    float2 crystal = V5Cells(p * float2(.32,.49) + V5Noise(p * .83) * .17);
    float width = .014 + pixel * .23;
    float fracture = (1-smoothstep(.003,width,crystal.y)) * smoothstep(.43,.74,V5Noise(p * .53));
    float scratches = smoothstep(.65,.90,V5Noise(wind * float2(.65,12))) * V5Resolved(pixel,12);
    float trappedAir = smoothstep(.62,.86,V5Noise(p * 4.7)) * (.34 + .66*V5Resolved(pixel,4.7));
    float deep = V5Noise(p * .22) * .77 + V5Noise(p * 1.36) * .23;
    half3 ice = lerp(half3(.027,.12,.174),half3(.088,.247,.309),deep);
    ice += half3(.24,.32,.36) * (.66*fracture + .15*trappedAir + .17*scratches);
    color = lerp(color,ice,iceMask);

    float snowRelief = .018*drift - .012*crust + .0027*grains;
    float roadRelief = .049*gravel + dust*(.012 - .003*crust);
    return lerp(lerp(snowRelief,roadRelief,roadWeight),.0015*deep - .002*fracture,iceMask);
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
    float asphalt = (V5Noise(p * 5.8) - .5) * V5Resolved(pixel, 5.8);
    float detail = V5SurfaceGrain(p,pixel);
    color = lerp(terrain * (1 + detail*.35),color * (1 + detail*.73 + asphalt*.16),roadWeight);
    return lerp(elevation + detail*.026, asphalt*.014 + detail*.039,roadWeight);
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
        return lerp(layers*.031 - cracks*.021,V5SurfaceGrain(p.xz,pixel)*.032,coat);
    }
    return layers * .031 - cracks * .021;
}

#endif
