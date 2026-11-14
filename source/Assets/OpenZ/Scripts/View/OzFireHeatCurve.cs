// OpenZ (GPL-3.0) - version 1.3: the heat curve of metal in fire. Plain C#, no Unity, so a plain .NET harness runs this same file.
namespace OpenZ
{
    /// <summary>
    /// How hot a unit in fire is, 0..1, in view time. First it soaks: SoakNeed seconds in fire before anything shows, falling at SoakFall out of fire, so a short lick shows
    /// nothing (one flame arrival counts as in fire for OzFireHeat.FlameGap, so SoakNeed holds that gap and 0.6 s of real flame). Then HeatUp seconds up. Out of fire it holds
    /// while sticky fire still burns on it and takes HeatDown seconds down. Water and death take WetDown and DeadDown seconds, and a wet or dead unit soaks nothing.
    /// </summary>
    public static class OzFireHeatCurve
    {
        public const float SoakNeed = 1.2f, SoakFall = 1f, HeatUp = 1.6f, HeatDown = 8f, WetDown = 1f, DeadDown = 1f;

        /// <summary>One step of dt seconds. dt 0 (a pause) changes nothing. The time past SoakNeed in this step already heats, so the curve is the same at any frame rate.</summary>
        public static void Step(ref float soak, ref float heat, float dt, bool inFire, bool sticky, bool wet, bool dead)
        {
            if (dt <= 0f) return;
            if (dead) { soak = 0f; heat = Toward(heat, 0f, dt / DeadDown); return; }
            if (wet) { soak = 0f; heat = Toward(heat, 0f, dt / WetDown); return; }
            if (inFire)
            {
                float over = soak + dt - SoakNeed;
                soak = Min(SoakNeed, soak + dt);
                if (over > 0f) heat = Toward(heat, 1f, Min(over, dt) / HeatUp);
                return;
            }
            soak = Max(0f, soak - dt * SoakFall);
            if (!sticky) heat = Toward(heat, 0f, dt / HeatDown);
        }

        /// <summary>Eased, as OzLavaWade.Push eases the legs in lava.</summary>
        public static float Smooth(float h) => h * h * (3f - 2f * h);

        /// <summary>The value for _OZLegHeat at heat h: never above peak.</summary>
        public static float Shown(float h, float peak) => peak * Smooth(h);

        /// <summary>Height of the glow band at heat h, t = its height at full heat. It grows as the lava's does.</summary>
        public static float Band(float h, float t) => t * (0.4f + 0.6f * Smooth(h));

        /// <summary>Where fire and lava meet the stronger stays, never a sum. Equal: the lava's own floats stay.</summary>
        public static bool FireWins(float shown, float lava) => shown > lava;

        static float Toward(float from, float to, float step) => from < to ? Min(to, from + step) : Max(to, from - step);
        static float Min(float a, float b) => a < b ? a : b;
        static float Max(float a, float b) => a > b ? a : b;
    }
}
