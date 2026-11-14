using System;

namespace OpenZ
{
    /// <summary>
    /// number for number, as that game ran it for its nuke. Plain C# (no UnityEngine), so the kit's proof compiles it alone and checks it
    /// against the source's own formulas (tools/timeline_replica.py).
    /// <list type="bullet">
    /// <item>Progress p = min(t / 5 x 0.72, 1): 0.144 a second, 1 at 6.94 s. The source drew it inside a 5 s effect and dropped it at
    /// 5 s (p 0.72, already a dark, nearly unseen glow there); here the same formula simply runs on to p = 1, where its own fade
    /// reaches 0, so it never pops.</item>
    /// <item>Opacity 0.75; brighter in the first second (0.75 + 0.25 (1 - t)); from p 0.7 down to 0 at p 1. The stem 0.15 less.</item>
    /// <item>
    /// in the same window; the stem fades first, every layer of it at once (<see cref="StemFade"/>: its alpha x 1 .. 0, smootherstep
    /// <see cref="StemFadeSecs"/>
    /// on screen the head goes when the shader cools it (decay smoothstep(<see cref="CoolFrom"/>, 1, p) from 3.47 s: a cooled point adds
    /// only the dark smoke colour, at full alpha), not when its opacity fades (p 0.7, 4.86 s): the stem is gone by p <see cref="StemGoneAt"/>
    /// (3.13 s), before the cooling starts. The life is the same.</item>
    /// <item>Rise (1 - (1 - p)^2) H; expand 1 - (1 - p)^4; the cap 0.75 H x expand wide, flattened lerp(1, 0.45, min(4p, 1)), turning
    /// 0.2 rad/s; the stem from 0.15 s, half the rise high in the middle, the rise tall, 0.3 x the cap wide.</item>
    /// <item>The shader's own clocks: the cap's noise at t x 0.5, the stem's at t x 0.8.</item>
    /// <item>H: the source's 36.2 units x a random size of 0.9 .. 1.2. Here H is set so that the mean cloud (size 1.05) spreads its cap
    /// to the editor's kill ring: H = ring / (0.75 x 1.05) x size (30 m ring: 40 m high, cap 30 m wide at size 1.05).</item>
    /// </list>
    /// </summary>
    public static class OzUeMushroomCurve
    {
        /// <summary>The source's numbers: max height (units), its effect's life (s), the slow-down it put on the cloud's progress.</summary>
        public const float SourceMaxHeight = 36.2f, SourceLife = 5f, SourcePace = 0.72f;
        /// <summary>Progress a second (0.144).</summary>
        public const float ProgressPerSec = SourcePace / SourceLife;
        /// <summary>The source's random size range and its middle.</summary>
        public const float SizeMin = 0.9f, SizeMax = 1.2f, SizeMean = (SizeMin + SizeMax) * 0.5f;
        /// <summary>Opacity: the base, the extra of the first second, where the fade starts (p), the stem's lower opacity.</summary>
        public const float BaseOpacity = 0.75f, FadeFrom = 0.7f, StemLess = 0.15f;
        /// <summary>
        /// shader cools the head (p <see cref="CoolFrom"/>): the stem fades from 2.13 to 3.13 s (was 1.5 s from 3.96 to 5.46 s, timed on the
        /// head's opacity fade at p 0.7, 4.86 s, while the cooling had already darkened the head).</summary>
        public const float StemFadeSecs = 1.0f, StemGoneAt = 0.45f;
        /// <summary>The shader's cooling start (progress): decay = smoothstep(CoolFrom, 1, p) (OzUeNukeMushroom.shader).</summary>
        public const float CoolFrom = 0.5f;
        /// <summary>The cap's width (x H), its flattest (y x), the stem's width (x the cap), the spin (rad/s), the stem's first moment (s).</summary>
        public const float CapWidth = 0.75f, Flattest = 0.45f, StemWidthK = 0.3f, Spin = 0.2f, StemFrom = 0.15f;
        /// <summary>The shader's clocks for the cap and the stem (x t).</summary>
        public const float CapClock = 0.5f, StemClock = 0.8f;
        /// <summary>The group stands this many source units above the ground (the source's blast point).</summary>
        public const float Lift = 1f;

        /// <summary>The whole life: p reaches 1 and the opacity 0 (6.94 s).</summary>
        public static float Life => 1f / ProgressPerSec;

        /// <summary>1.4: when the head's opacity fade starts (s; p = FadeFrom: 4.86 s), when the shader starts to cool it (p = CoolFrom: 3.47 s),
        /// when the stem's fade starts (2.13 s) and ends (p = StemGoneAt: 3.13 s).</summary>
        public static float HeadFadeFrom => FadeFrom / ProgressPerSec;
        public static float HeadCoolFrom => CoolFrom / ProgressPerSec;
        public static float StemFadeFrom => StemFadeEnd - StemFadeSecs;
        public static float StemFadeEnd => StemGoneAt / ProgressPerSec;

        /// <summary>The cloud's height H (m) for a size (0.9 .. 1.2) and the ring (m) the mean cloud's cap spreads to.</summary>
        public static float HeightFor(float size, float ringM) => ringM / (CapWidth * SizeMean) * size;

        /// <summary>Metres a source unit for a cloud of height h and size s (for the lift).</summary>
        public static float MetresPerUnit(float h, float size) => h / (SourceMaxHeight * size);

        public static float Progress(float t) => Math.Min(Math.Max(t, 0f) * ProgressPerSec, 1f);

        public static float Opacity(float t)
        {
            float p = Progress(t), o = BaseOpacity;
            if (t < 1f) o = BaseOpacity + (1f - BaseOpacity) * (1f - t);
            if (p > FadeFrom) o = BaseOpacity * (1f - Smootherstep((p - FadeFrom) / (1f - FadeFrom)));
            return Math.Max(0f, o);
        }

        public static float StemOpacity(float t) => Math.Max(0f, Opacity(t) - StemLess);

        /// <summary><see cref="StemFadeFrom"/><see cref="StemFadeSecs"/>
        /// It multiplies the stem's alpha (the shader's _Fade), so its fire, glow, core and smoke all go together, before the head thins.</summary>
        public static float StemFade(float t) => 1f - Smootherstep((t - StemFadeFrom) / StemFadeSecs);

        /// <summary>6x^5 - 15x^4 + 10x^3 of x clamped to 0 .. 1.</summary>
        public static float Smootherstep(float x) { x = Math.Min(Math.Max(x, 0f), 1f); return x * x * x * (x * (x * 6f - 15f) + 10f); }

        public static float Rise(float t, float h) { float q = 1f - Progress(t); return (1f - q * q) * h; }

        public static float Expand(float t) { float q = 1f - Progress(t); return 1f - q * q * q * q; }

        public static float Flatten(float t) => 1f + (Flattest - 1f) * Math.Min(Progress(t) * 4f, 1f);

        /// <summary>The cap's radius (m): its x and z scale on the unit sphere.</summary>
        public static float CapRadius(float t, float h) => h * CapWidth * Expand(t);

        public static float StemRadius(float t, float h) => CapRadius(t, h) * StemWidthK;

        public static float StemHeight(float t, float h) => Math.Max(0.1f, Rise(t, h));

        public static bool StemShown(float t) => t > StemFrom && t < StemFadeEnd;                     // 1.4: not drawn once its fade is done

        /// <summary>The cap's turn (rad) about the vertical.</summary>
        public static float CapTurn(float t) => t * Spin;

        public static float CapTime(float t) => t * CapClock;

        public static float StemTime(float t) => t * StemClock;
    }
}
