// OpenZ/FlagCloth (the brass ball on the pole's top = the lamp, the cloth glowing from inside), OpenZ/FlagMarks (the lamp's halo,
// the enemy flags' day rings) and OpenZ/FlagNightPool (the neutral flags' pool of light).
#ifndef OZ_FLAG_LAMP_INCLUDED
#define OZ_FLAG_LAMP_INCLUDED

// global, FlagView every frame (0 when no FlagView runs):
//   x = the flags' night, 0 by day .. 1 at night (up with the street lamps at dusk, down at dawn, smooth: FlagView.NightOf)
//   y = the lamp's HDR gain, z = an owned cloth's glow from inside, w = a neutral cloth lit by its pool of light
float4 _OZFlagLook;

// A radio mast's warning lamp: one flash every 1.6 s (up in 60 ms, held, a soft 0.4 s tail), a faint glow between the flashes.
// seedRad = the flag's own phase (its cloth wave seed, radians): neighbouring flags do not flash in step.
half OZFlagBlink(float seconds, float seedRad)
{
    const float period = 1.6;
    float s = frac(seconds / period + seedRad * 0.1591549) * period;
    float on = smoothstep(0.0, 0.06, s) * (1.0 - smoothstep(0.16, 0.56, s));
    return (half)(0.08 + 0.92 * on);
}

#endif
