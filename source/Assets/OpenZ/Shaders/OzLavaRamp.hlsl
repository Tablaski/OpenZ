// OpenZ (GPL-3.0) - version 1.3, level 7: the one colour ramp of everything hot (LV_CONTRACT.md section 7). The lava surface, the veins, the
// domes and rings, the volcano flow, the bombs and the robots' legs all read their colour here. The stop colours are not in this file:
// they are the global _OZLavaRamp, written once at level start by OzLavaRampTable (View/OzLavaShared.cs), so the ramp is set against
// captured frames in Play with no shader recompile. No keyword, no texture. On a level with no lava nothing calls this.
//   T (temperature)  0 crust dark .. 0.25 warm crust .. 0.5 dull red .. 0.75 molten .. 1 core; above 1 the core colour gets brighter (HDR)
#ifndef OZ_LAVA_RAMP_INCLUDED
#define OZ_LAVA_RAMP_INCLUDED

float4 _OZLavaRamp[5];   // global: linear rgb, a = HDR gain of that stop

/// The colour of temperature T, with its HDR gain.
half3 OzLavaRamp(half T)
{
    half s = saturate(T) * 4.0;
    int i = min((int)s, 3);
    half f = s - (half)i;
    half3 a = (half3)_OZLavaRamp[i].rgb * (half)_OZLavaRamp[i].a, b = (half3)_OZLavaRamp[i + 1].rgb * (half)_OZLavaRamp[i + 1].a;
    return lerp(a, b, f) * (1.0 + 1.6 * max(T - 1.0, 0.0));
}

/// 0 = solid crust (the surface lights it by the sun), 1 = pure emission.
half OzLavaGlow(half T) { return smoothstep(0.25, 0.5, T); }

/// The same colour as light given off: strongest at night. Each shader passes its own _OZNight (TeamLit and the FX shaders declare it themselves).
half3 OzLavaEmit(half T, half night) { return OzLavaRamp(T) * OzLavaGlow(T) * (1.0 + 0.15 * saturate(night)); }

#endif
