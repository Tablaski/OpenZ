// OpenZ 1.5 cloud_shadows: soft shadows of drifting clouds on the ground (the classic RTS look), shared by OpenZ/Sand (the L02, L07, L17
// ground), OpenZ/JungleGround (the L16 ground) and OpenZ/TeamLit (environment class only: rocks, cliffs, map-edge skirts, bridges, trees and
// palette props, the same class the rain wetness darkens). Units, vehicles, guns, buildings and map items are never touched. Include AFTER
// Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl.
//
// Globals only (no material property, SRP Batcher compatible), written every frame by View/OzCloudShadows.cs while a level that has cloud
// shadows runs, and _OzCloudParams.x set back to 0 when it ends: x = 0 means every function below returns "no cloud" without reading a texture
// (a uniform branch), so the menu, the Unit editor, every level without a row and the editor's own views stay exactly as they are.
//   _OzCloudTex     the cloud picture (Resources/OpenZ/FX/OzCloudShadow.png: 512 x 512 grey, tiles seamlessly, histogram-equalised)
//   _OzCloudA       x = picture repeats per metre of layer A (1 / tile), y = TeamLit's units' share (cloud_shadows_visible), zw = layer A's drift offset (in tiles, kept in 0..1)
//   _OzCloudB       the same for layer B (its frame turned 37 deg against A, so the two never line up and the shapes slowly change)
//   _OzCloudParams  x = strength: the share of the direct sun a full cloud takes (0 = off), y = threshold, z = softness, w = layer B's weight
// What a cloud does: it takes x of the DIRECT sun (diffuse) where the sun shines; places already in a cast shadow do not change (a cloud over a
// building's shadow is not darker), the sky light, the lamps, the fog of war, emission and lava glow stay as they are. On the sand the warm
// shadow bounce of OpenZ/Sand comes back in the same share, so a cloud shadow has the same warm colour as every other shadow on the sand.
#ifndef OPENZ_CLOUD_SHADOW_INCLUDED
#define OPENZ_CLOUD_SHADOW_INCLUDED

// The sampler: by default the picture's own (Repeat, trilinear, from its import settings). A shader that is short of sampler slots (Direct3D 11
// has 16) defines OZ_CLOUD_SAMPLER as one of its own Repeat + mip-mapped samplers before the include: OpenZ/JungleGround reads it with
// sampler_GroundNoise (its noise picture: Repeat, trilinear), so the include adds no sampler there.
TEXTURE2D(_OzCloudTex);
#ifndef OZ_CLOUD_SAMPLER
    SAMPLER(sampler_OzCloudTex);
    #define OZ_CLOUD_SAMPLER sampler_OzCloudTex
#endif
float4 _OzCloudA;
float4 _OzCloudB;
float4 _OzCloudParams;

// 0..1: the share of the direct sun the clouds take at this world point, times `on` (0 = never: a class the clouds skip). The clouds lie far up,
// so a raised point (a cliff top, a rock face) reads the cloud along the sun's direction: its shadow lines up with the cast shadows.
half OZCloudShade(float3 positionWS, half on)
{
    float3 L = _MainLightPosition.xyz;
    float2 p = positionWS.xz - L.xz / max(L.y, 0.25) * positionWS.y;
    float2 q = float2(0.79864 * p.x - 0.60181 * p.y, 0.60181 * p.x + 0.79864 * p.y);   // layer B's frame, turned 37 deg
    float2 ua = p * _OzCloudA.x + _OzCloudA.zw;
    float2 ub = q * _OzCloudB.x + _OzCloudB.zw;
    float2 dax = ddx(ua), day = ddy(ua), dbx = ddx(ub), dby = ddy(ub);              // derivatives outside the branch (the reads below are explicit-gradient)
    half k = 0;
    [branch] if (_OzCloudParams.x * on > 0.001)
    {
        half a = SAMPLE_TEXTURE2D_GRAD(_OzCloudTex, OZ_CLOUD_SAMPLER, ua, dax, day).r;
        half b = SAMPLE_TEXTURE2D_GRAD(_OzCloudTex, OZ_CLOUD_SAMPLER, ub, dbx, dby).r;
        half n = lerp(a, b, (half)_OzCloudParams.w);
        half t = (half)_OzCloudParams.y, s = (half)max(_OzCloudParams.z, 0.01);
        k = (half)(_OzCloudParams.x * on) * smoothstep(t - s, t + s, n);
    }
    return k;
}

// OpenZ/Sand: the caller already has the direct sun's diffuse light at this pixel (albedo x sun colour x N.L x its own soft shadow) and the
// warm shadow bounce it adds in shadow (lift = _ShadowTint x _ShadowLift): the cloud takes its share of the first and gives back the same share
// of the second. Returns the change to add to the colour (0 with no cloud).
half3 OZCloudSun(float3 positionWS, half3 sunDiffuse, half3 lift)
{
    return sunDiffuse * (lift - (half3)1) * OZCloudShade(positionWS, (half)1);
}

// The direct sun's visibility from the main shadow map with ONE hardware compare tap (2 x 2 filtered), the far shadow fade as URP applies it.
// Used only under a cloud (OZCloudSunLit's branch): the change there is a share of the sun, so the soft filter's few extra texels of edge
// softness make no visible difference, and the soft filter's 9 to 16 taps are not paid twice.
half OZCloudSunVis(float4 shadowCoord, float3 positionWS)
{
#if !defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    return (half)1;
#else
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
        // URP's SampleScreenSpaceShadowmap, read at mip 0 (no screen derivatives: this runs inside a branch that differs per pixel)
        float2 suv = UnityStereoTransformScreenSpaceTex(shadowCoord.xy / max(0.00001, shadowCoord.w));
        half a = (half)SAMPLE_TEXTURE2D_X_LOD(_ScreenSpaceShadowmapTexture, sampler_PointClamp, suv, 0).x;
    #else
        half a = (half)SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture, sampler_LinearClampCompare, shadowCoord.xyz);
        a = LerpWhiteTo(a, GetMainLightShadowParams().x);
        a = (BEYOND_SHADOW_FAR(shadowCoord)) ? (half)1 : a;
    #endif
    return lerp(a, (half)1, GetMainLightShadowFade(positionWS));
#endif
}

// OpenZ/JungleGround and OpenZ/TeamLit (environment class): the change to add to the colour for the cloud at this pixel: minus the cloud's share
// of the direct sun's diffuse light. albedoKey = the albedo times the material's own key tint (TeamLit's stylized warm key, else 1); on = 1 for
// a class the clouds reach, 0 for every other (nothing is read then).
half3 OZCloudSunLit(float3 positionWS, float4 shadowCoord, half3 normalWS, half3 albedoKey, half on)
{
    half k = OZCloudShade(positionWS, on);
    half3 r = 0;
    [branch] if (k > 0.001)
    {
        Light l = GetMainLight();
        half ndl = saturate(dot(normalWS, l.direction));
        r = -albedoKey * l.color * (ndl * OZCloudSunVis(shadowCoord, positionWS) * k);
    }
    return r;
}

#endif
