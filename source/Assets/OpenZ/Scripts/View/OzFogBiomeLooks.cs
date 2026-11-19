// OpenZ (GPL-3.0) - version 1.4, FOG OF WAR: the MAYHEM fog's look chosen by BIOME (the owner's cloud pick, 6 Nov 2026). Numbers only, no Unity.
using System;

namespace OpenZ
{
    /// <summary>
    /// <list type="bullet">
    /// <item>A level not in the table takes the biome its map names (<see cref="ByPlanet"/>, the level start: BiomeOf(level, planet)).</item>
    /// <item>Each level names its biome (<see cref="LevelIds"/> / <see cref="LevelBiome"/>); a new level of a biome adds one row there and gets
    /// that biome's look. A level not in the table is a DESERT level (1.4's rule before this kit: every level but L16, L17 and L07).</item>
    /// <item>A biome's look (<see cref="Biomes"/>): the cloud style (by its name in View/OzFogOfWar.Looks.cs), the colour idea LAND (the ground's
    /// colour, OzFogOfWar.Palette.Land) and the deep fog's cover range.</item>
    /// <item>The easing (<see cref="Cover"/>): the cover drifts back and forth between its two values on an eased sine (zero speed at both ends,
    /// never a fixed value, never a jump), one full swing in <see cref="PeriodS"/>, each level starting at its own point of the swing
    /// (<see cref="PhaseOf"/>). The clock is the fog's own frame clock (OzFogOfWar.FowDt), so a recorded clip shows the real speed.</item>
    /// <item>View only: the cover changes how much of the ground shows through the deep cloud; what is explored, what is hidden and every rule
    /// stay as they are. MAYHEM only (<see cref="CoverFor"/>, <see cref="ColourFor"/>): every other match keeps today's numbers.</item>
    /// </list>
    /// The biome numbers are the palette numbers of OzFogOfWar (Desert 0, Jungle 1, City 2, Volcano 3 = LAVA, Arctic 4).
    /// </summary>
    public static class OzFogBiomeLooks
    {
        public const int Desert = 0, Jungle = 1, City = 2, Lava = 3, Arctic = 4;

        public struct Look
        {
            /// <summary>The biome's name, its cloud style (a Looks[i].Name), its colour idea (a ColourNames[i]), the cover's two values (0..1).</summary>
            public string Biome, Style, Colour;
            public float CoverMin, CoverMax;
        }

        /// <summary></summary>
        public static readonly Look[] Biomes =
        {
            new Look { Biome = "DESERT", Style = "MIST LAYERS", Colour = "LAND", CoverMin = 0.85f, CoverMax = 1f },
            new Look { Biome = "JUNGLE", Style = "CLOUD BANKS", Colour = "LAND", CoverMin = 0.85f, CoverMax = 1f },
            new Look { Biome = "CITY",   Style = "CLOUD BANKS", Colour = "LAND", CoverMin = 0.90f, CoverMax = 1f },
            new Look { Biome = "LAVA",   Style = "CLOUD BANKS", Colour = "LAND", CoverMin = 0.90f, CoverMax = 1f },
            new Look { Biome = "ARCTIC", Style = "MIST LAYERS", Colour = "LAND", CoverMin = 0.90f, CoverMax = 1f },   // the next update's biome (no level yet)
        };

        /// <summary>Each level's biome. A new level: one id and one biome here (an arctic level: Arctic).</summary>
        public static readonly string[] LevelIds = { "L02", "L07", "L16", "L17", "L18" };
        public static readonly int[] LevelBiome = { Desert, Lava, Jungle, City, City };
        /// <summary>A level not in the table.</summary>
        public const int Fallback = Desert;

        /// <summary>One full swing (low, high, low again), seconds of the fog's clock.</summary>
        public const double PeriodS = 56.0;
        /// <summary>The checks' bar for one frame's change of the cover (0.25 percentage points; the fog's clock takes at most 0.25 s a frame:
        /// the largest step is 0.15 * pi / 56 * 0.25 = 0.0021).</summary>
        public const float MaxStepPerFrame = 0.0025f;

        public static int BiomeOf(string level)
        {
            for (int i = 0; i < LevelIds.Length; i++) if (LevelIds[i] == level) return LevelBiome[i];
            return Fallback;
        }

        /// <summary>The biome a map names itself (its grid file's "planet": desert, jungle, city, volcanic; an arctic map: arctic / ice / snow /
        /// winter), -1 for none or an unknown name.</summary>
        public static int ByPlanet(string planet)
        {
            switch (planet == null ? "" : planet.Trim().ToLowerInvariant())
            {
                case "desert": return Desert;
                case "jungle": return Jungle;
                case "city": return City;
                case "volcanic": case "volcano": case "lava": return Lava;
                case "arctic": case "ice": case "snow": case "winter": return Arctic;
                default: return -1;
            }
        }

        /// <summary>The level's biome at the level start: its row in the table; a level not in the table takes the biome its map names (review,
        /// 6 Nov: a future jungle or arctic map gets its biome's look even before it has a row); else DESERT.</summary>
        public static int BiomeOf(string level, string planet)
        {
            for (int i = 0; i < LevelIds.Length; i++) if (LevelIds[i] == level) return LevelBiome[i];
            int p = ByPlanet(planet);
            return p >= 0 ? p : Fallback;
        }

        public static int Clamp(int biome) => biome < 0 || biome >= Biomes.Length ? Fallback : biome;

        /// <summary>The level's start point on the swing, 0..1 (FNV-1a of its id; the same every match and on every PC).</summary>
        public static double PhaseOf(string level)
        {
            uint h = 2166136261u;
            if (level != null) foreach (char c in level) { h ^= c; h *= 16777619u; }
            return (h % 1000u) / 1000.0;
        }

        /// <summary>The deep fog's cover at the fog's clock t (s) for a biome and a level's phase: an eased sine between CoverMin and CoverMax.</summary>
        public static float Cover(int biome, double t, double phase)
        {
            var b = Biomes[Clamp(biome)];
            double x = t / PeriodS + phase; x -= Math.Floor(x);
            double k = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * x);
            float v = (float)(b.CoverMin + (b.CoverMax - b.CoverMin) * k);
            return v < b.CoverMin ? b.CoverMin : v > b.CoverMax ? b.CoverMax : v;
        }

        /// <summary>The fastest change of a biome's cover (per second of the fog's clock).</summary>
        public static double MaxRatePerS(int biome) { var b = Biomes[Clamp(biome)]; return (b.CoverMax - b.CoverMin) * Math.PI / PeriodS; }

        /// <summary>The cover on screen: the biome's eased cover in a MAYHEM match, unless the testing panel set one (manual) or a check set a
        /// cover under 100 %; every other match: the session's cover as before the kit (1 = today).</summary>
        public static float CoverFor(bool mayhem, bool manual, float manualCover, float eased) => mayhem && !manual && manualCover >= 1f ? eased : manualCover;

        /// <summary>The colour idea on screen: the biome's (LAND) in a MAYHEM match, unless the testing panel set one or a check set another
        /// than TODAY; every other match: the session's colour as before the kit (TODAY).</summary>
        public static int ColourFor(bool mayhem, bool manual, int manualColour, int biomeColour, int today) => mayhem && !manual && manualColour == today ? biomeColour : manualColour;
    }
}
